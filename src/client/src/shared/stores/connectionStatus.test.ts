import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { GameClient } from '$shared/net/GameClient';
import { bootstrapGameStore, connectionStatus } from './gameStore';

/**
 * The client has always exposed onConnect/onDisconnect and nothing subscribed to them, so a
 * dropped session was invisible: the party stopped responding to input with no indication why.
 * That matters more now that input made while disconnected is dropped rather than queued — the
 * player has to be told the session is down, or the game simply looks broken.
 */
class StubClient {
  onStateCb: ((s: unknown) => void) | null = null;
  onConnectCb: (() => void) | null = null;
  onDisconnectCb: (() => void) | null = null;

  onState(cb: (s: unknown) => void) {
    this.onStateCb = cb;
  }
  onConnect(cb: () => void) {
    this.onConnectCb = cb;
  }
  onDisconnect(cb: () => void) {
    this.onDisconnectCb = cb;
  }
  onError() {}
  sendAction() {}
  connect() {}
  disconnect() {}
}

function bootstrap(): StubClient {
  const client = new StubClient();
  bootstrapGameStore(client as unknown as GameClient);
  return client;
}

describe('connection status', () => {
  beforeEach(() => {
    connectionStatus.set('connecting');
  });

  it('starts out connecting, before any server traffic', () => {
    bootstrap();
    expect(get(connectionStatus)).toBe('connecting');
  });

  /**
   * An open socket is not a usable session: the client still has to complete the ready handshake
   * before the server will accept an action. Reporting "connected" on socket open would tell the
   * player their input counts while it is still being held.
   */
  it('stays connecting until the first state arrives, not merely on socket open', () => {
    const client = bootstrap();

    client.onConnectCb?.();
    expect(get(connectionStatus)).toBe('connecting');

    client.onStateCb?.({ mode: 'Town' });
    expect(get(connectionStatus)).toBe('connected');
  });

  it('reports disconnected when the socket drops', () => {
    const client = bootstrap();
    client.onStateCb?.({ mode: 'Town' });

    client.onDisconnectCb?.();

    expect(get(connectionStatus)).toBe('disconnected');
  });

  it('returns to connected once a reconnect delivers state again', () => {
    const client = bootstrap();
    client.onStateCb?.({ mode: 'Town' });
    client.onDisconnectCb?.();

    client.onConnectCb?.();
    expect(get(connectionStatus)).toBe('connecting');

    client.onStateCb?.({ mode: 'Town' });
    expect(get(connectionStatus)).toBe('connected');
  });
});
