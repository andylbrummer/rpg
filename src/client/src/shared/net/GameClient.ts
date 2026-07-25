import type { GameState, PlayerAction, ProtocolEnvelope, ErrorPayload, AnalyticsData } from '$shared/types/game';

export class GameClient {
  private ws: WebSocket | null = null;
  private reconnectAttempts = 0;
  // Retry indefinitely with capped backoff: a dev backend restart (or any transient
  // drop) should auto-heal without a manual page reload. Backoff is capped at 30s.
  private reconnectClosed = false;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private nextSeq = 1;
  private isReady = false;
  private actionQueue: PlayerAction[] = [];
  private onStateCallback: ((state: GameState) => void) | null = null;
  private onConnectCallback: (() => void) | null = null;
  private onDisconnectCallback: (() => void) | null = null;
  private onErrorCallback: ((error: ErrorPayload) => void) | null = null;
  private onAnalyticsCallback: ((data: AnalyticsData) => void) | null = null;


  connect(): void {
    const wsUrl = `ws://${window.location.host}/ws`;
    this.reconnectClosed = false;
    this.clearReconnectTimer();

    // Tear down any previous socket first. Connecting over a live one used to abandon it still
    // open, with its handlers attached: it leaked the connection, and whenever that orphan
    // eventually closed its onclose started a second, independent retry chain alongside this
    // one.
    this.teardownSocket();

    try {
      this.ws = new WebSocket(wsUrl);

      this.ws.onopen = () => {
        this.reconnectAttempts = 0;
        this.onConnectCallback?.();
      };

      this.ws.onmessage = (event) => {
        this.handleMessage(event.data);
      };

      this.ws.onclose = () => {
        this.isReady = false;
        // Drop any actions queued against the dead socket — replaying stale input
        // (e.g. clicks made while disconnected) after reconnect would corrupt state.
        this.actionQueue = [];
        this.onDisconnectCallback?.();
        this.attemptReconnect();
      };

      this.ws.onerror = (err) => {
        console.error('WebSocket error:', err);
      };
    } catch (err) {
      console.error('Failed to create WebSocket:', err);
    }
  }

  private handleMessage(data: string): void {
    try {
      const envelope = JSON.parse(data) as ProtocolEnvelope;
      if (envelope.v !== 2) {
        console.error('Unsupported protocol version:', envelope.v);
        return;
      }

      switch (envelope.type) {
        case 'hello': {
          const payload = envelope.payload as { protocolVersion: number; sessionId: string };
          if (payload.protocolVersion !== 2) {
            console.error('Unsupported protocol version from server:', payload.protocolVersion);
            this.ws?.close();
            return;
          }
          this.sendEnvelope('ready', {});
          break;
        }

        case 'state': {
          const wasReady = this.isReady;
          this.isReady = true;
          this.onStateCallback?.(envelope.payload as unknown as GameState);
          if (!wasReady) {
            this.flushActionQueue();
          }
          break;
        }

        case 'error': {
          const error = envelope.payload as unknown as ErrorPayload;
          this.onErrorCallback?.(error);
          break;
        }

        case 'heartbeat.ping': {
          const pingPayload = envelope.payload as { pingSeq: number };
          this.sendEnvelope('heartbeat.pong', { pingSeq: pingPayload.pingSeq });
          break;
        }

        case 'content.reload': {
          // Dev-only: content hot-reload notification. Ignored in production.
          console.info('Content reload:', envelope.payload);
          break;
        }

        case 'analytics.data': {
          this.onAnalyticsCallback?.(envelope.payload as unknown as AnalyticsData);
          break;
        }

        default:
          console.warn('Unknown envelope type:', envelope.type);
      }
    } catch (err) {
      console.error('Failed to parse message:', err);
    }
  }

  /** Returns false when the socket was not in a state to accept the message. */
  private sendEnvelope(type: string, payload: Record<string, unknown>): boolean {
    if (this.ws?.readyState !== WebSocket.OPEN) return false;

    const envelope: ProtocolEnvelope = {
      v: 2,
      type,
      seq: this.nextSeq++,
      payload,
    };

    this.ws.send(JSON.stringify(envelope));
    return true;
  }

  private attemptReconnect(): void {
    if (this.reconnectClosed) return; // explicit disconnect() — stop retrying
    this.reconnectAttempts++;
    // Exponential backoff capped at 30s; keep retrying forever so a backend
    // restart recovers on its own instead of bricking the session.
    const delay = Math.min(Math.pow(2, Math.min(this.reconnectAttempts, 5)) * 1000, 30000);
    this.clearReconnectTimer();
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      // Re-check: disconnect() may have been called while this backoff was pending, and
      // connect() clears the closed flag, so without this the reconnect would resurrect a
      // session the caller deliberately ended.
      if (this.reconnectClosed) return;
      this.connect();
    }, delay);
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  /**
   * Detaches this socket's handlers before closing it, so its onclose cannot drive reconnect
   * logic for a connection we have already replaced or abandoned.
   */
  private teardownSocket(): void {
    const socket = this.ws;
    if (!socket) return;

    socket.onopen = null;
    socket.onmessage = null;
    socket.onclose = null;
    socket.onerror = null;
    this.ws = null;

    if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
      socket.close();
    }
  }

  disconnect(): void {
    this.reconnectClosed = true;
    this.clearReconnectTimer();
    this.isReady = false;
    this.actionQueue = [];
    this.teardownSocket();
  }

  sendAction(action: PlayerAction): void {
    // Queue rather than drop when the send does not land: isReady can still be true for the
    // moment between the socket closing and onclose running, and an action silently lost
    // there is an input the player made that the game never sees.
    if (!this.isReady || !this.sendEnvelope('action', action as unknown as Record<string, unknown>)) {
      this.actionQueue.push(action);
    }
  }

  private flushActionQueue(): void {
    while (this.actionQueue.length > 0) {
      const action = this.actionQueue[0];
      if (!this.sendEnvelope('action', action as unknown as Record<string, unknown>)) {
        // Socket went away mid-flush. Leave the rest queued, in order, for the next flush.
        return;
      }
      this.actionQueue.shift();
    }
  }

  onState(callback: (state: GameState) => void): void {
    this.onStateCallback = callback;
  }

  onConnect(callback: () => void): void {
    this.onConnectCallback = callback;
  }

  onDisconnect(callback: () => void): void {
    this.onDisconnectCallback = callback;
  }

  onError(callback: (error: ErrorPayload) => void): void {
    this.onErrorCallback = callback;
  }

  onAnalytics(callback: (data: AnalyticsData) => void): void {
    this.onAnalyticsCallback = callback;
  }

  requestAnalytics(): void {
    this.sendEnvelope('analytics.request', {});
  }
}
