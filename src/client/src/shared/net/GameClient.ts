import type { GameState, PlayerAction, ProtocolEnvelope, ErrorPayload, AnalyticsData } from '$shared/types/game';

export class GameClient {
  private ws: WebSocket | null = null;
  private serverPort: number;
  private reconnectAttempts = 0;
  // Retry indefinitely with capped backoff: a dev backend restart (or any transient
  // drop) should auto-heal without a manual page reload. Backoff is capped at 30s.
  private reconnectClosed = false;
  private nextSeq = 1;
  private isReady = false;
  private actionQueue: PlayerAction[] = [];
  private onStateCallback: ((state: GameState) => void) | null = null;
  private onConnectCallback: (() => void) | null = null;
  private onDisconnectCallback: (() => void) | null = null;
  private onErrorCallback: ((error: ErrorPayload) => void) | null = null;
  private onAnalyticsCallback: ((data: AnalyticsData) => void) | null = null;


  constructor(serverPort?: number) {
    this.serverPort = serverPort || (window as any).SERVER_PORT || 19421;
  }

  connect(): void {
    const wsUrl = `ws://${window.location.host}/ws`;
    this.reconnectClosed = false;

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

  private sendEnvelope(type: string, payload: Record<string, unknown>): void {
    if (this.ws?.readyState !== WebSocket.OPEN) return;

    const envelope: ProtocolEnvelope = {
      v: 2,
      type,
      seq: this.nextSeq++,
      payload,
    };

    this.ws.send(JSON.stringify(envelope));
  }

  private attemptReconnect(): void {
    if (this.reconnectClosed) return; // explicit disconnect() — stop retrying
    this.reconnectAttempts++;
    // Exponential backoff capped at 30s; keep retrying forever so a backend
    // restart recovers on its own instead of bricking the session.
    const delay = Math.min(Math.pow(2, Math.min(this.reconnectAttempts, 5)) * 1000, 30000);
    setTimeout(() => this.connect(), delay);
  }

  disconnect(): void {
    this.reconnectClosed = true;
    this.ws?.close();
    this.ws = null;
  }

  sendAction(action: PlayerAction): void {
    if (this.isReady) {
      this.sendEnvelope('action', action as unknown as Record<string, unknown>);
    } else {
      this.actionQueue.push(action);
    }
  }

  private flushActionQueue(): void {
    while (this.actionQueue.length > 0) {
      const action = this.actionQueue.shift();
      if (action) {
        this.sendEnvelope('action', action as unknown as Record<string, unknown>);
      }
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
