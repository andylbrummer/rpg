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
    console.log(`GameClient using server port: ${this.serverPort}`);
  }

  connect(): void {
    const wsUrl = `ws://localhost:${this.serverPort}/`;
    console.log('Connecting to WebSocket:', wsUrl);
    
    try {
      this.ws = new WebSocket(wsUrl);
      
      this.ws.onopen = () => {
        console.log('WebSocket connected successfully');
        this.reconnectAttempts = 0;
        this.onConnectCallback?.();
      };

      this.ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          console.log('Received message:', data.type);
          if (data.type === 'state') {
            this.onStateCallback?.(data as GameState);
          }
        } catch (err) {
          console.error('Failed to parse message:', err);
        }
      };

      this.ws.onclose = (event) => {
        console.log('WebSocket closed:', event.code, event.reason);
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
      console.log(`Reconnecting... attempt ${this.reconnectAttempts}`);
      setTimeout(() => this.connect(), 1000 * this.reconnectAttempts);
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
      console.log('Sending action:', action.type);
      this.ws.send(JSON.stringify(action));
    } else {
      console.warn('Cannot send action, WebSocket not open');
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

  async fetchContent<T>(endpoint: string): Promise<T> {
    const response = await fetch(`http://localhost:${this.serverPort}/api/${endpoint}`);
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    return response.json() as Promise<T>;
  }
}
