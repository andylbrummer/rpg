import type { GameState, PlayerAction } from '../types/game';

export class GameClient {
  private ws: WebSocket | null = null;
  private serverPort: number;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private onStateCallback: ((state: GameState) => void) | null = null;
  private onConnectCallback: (() => void) | null = null;
  private onDisconnectCallback: (() => void) | null = null;

  constructor(serverPort?: number) {
    // Priority: explicit port > window.SERVER_PORT > default 8080
    this.serverPort = serverPort || (window as any).SERVER_PORT || 19421;
  }

  connect(): void {
    const wsUrl = `ws://${window.location.host}/ws`;

    try {
      this.ws = new WebSocket(wsUrl);
      
      this.ws.onopen = () => {
        this.reconnectAttempts = 0;
        this.onConnectCallback?.();
      };

      this.ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          if (data.type === 'state') {
            this.onStateCallback?.(data as GameState);
          }
        } catch (err) {
          console.error('Failed to parse message:', err);
        }
      };

      this.ws.onclose = (event) => {
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

  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++;
      const delay = Math.min(Math.pow(2, this.reconnectAttempts) * 1000, 30000);
      setTimeout(() => this.connect(), delay);
    } else {
      console.error('Max reconnect attempts reached');
    }
  }

  disconnect(): void {
    this.ws?.close();
    this.ws = null;
  }

  sendAction(action: PlayerAction): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(action));
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
}
