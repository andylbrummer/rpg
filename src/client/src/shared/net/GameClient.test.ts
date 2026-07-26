import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { GameClient } from './GameClient';

/**
 * Minimal stand-in for the browser WebSocket. Every instance registers itself so a test can
 * drive the lifecycle (open, close, inbound frames) and inspect what was sent.
 */
class FakeWebSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;

  static instances: FakeWebSocket[] = [];

  readyState: number = FakeWebSocket.CONNECTING;
  sent: string[] = [];
  closeCalls = 0;

  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: ((err: unknown) => void) | null = null;

  constructor(public url: string) {
    FakeWebSocket.instances.push(this);
  }

  send(data: string): void {
    this.sent.push(data);
  }

  close(): void {
    this.closeCalls++;
    this.readyState = FakeWebSocket.CLOSED;
  }

  /** Completes the handshake and delivers the server hello. */
  open(): void {
    this.readyState = FakeWebSocket.OPEN;
    this.onopen?.();
  }

  receive(envelope: unknown): void {
    this.onmessage?.({ data: JSON.stringify(envelope) });
  }

  /** Drops the connection the way a server restart or a network blip would. */
  drop(): void {
    this.readyState = FakeWebSocket.CLOSED;
    this.onclose?.();
  }

  sentTypes(): string[] {
    return this.sent.map((raw) => JSON.parse(raw).type);
  }
}

const HELLO = { v: 2, type: 'hello', seq: 0, payload: { protocolVersion: 2, sessionId: 'abc' } };
const STATE = { v: 2, type: 'state', seq: 1, payload: { mode: 'Town' } };

function latest(): FakeWebSocket {
  return FakeWebSocket.instances[FakeWebSocket.instances.length - 1];
}

describe('GameClient connection lifecycle', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    FakeWebSocket.instances = [];
    (globalThis as unknown as { WebSocket: unknown }).WebSocket = FakeWebSocket;
    (globalThis as unknown as { window: unknown }).window = { location: { host: 'localhost:19421' } };
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reconnects after the socket drops', () => {
    const client = new GameClient();
    client.connect();
    latest().open();

    expect(FakeWebSocket.instances).toHaveLength(1);

    latest().drop();
    vi.advanceTimersByTime(60_000);

    expect(FakeWebSocket.instances.length).toBeGreaterThan(1);
  });

  it('opens exactly one replacement socket per drop', () => {
    const client = new GameClient();
    client.connect();

    for (let round = 0; round < 4; round++) {
      const before = FakeWebSocket.instances.length;
      latest().open();
      latest().drop();
      vi.advanceTimersByTime(60_000);
      expect(FakeWebSocket.instances.length).toBe(before + 1);
    }
  });

  /**
   * Connecting over a live socket used to abandon it still open with its handlers attached.
   * That leaks the connection, and when the orphan later closes its onclose starts a second
   * retry chain running alongside the real one.
   */
  it('closes the previous socket when connect is called again', () => {
    const client = new GameClient();
    client.connect();
    const first = latest();
    first.open();

    client.connect();
    const second = latest();
    second.open();

    expect(second).not.toBe(first);
    expect(first.closeCalls).toBe(1);

    // The orphan closing must not drive any reconnect of its own.
    const beforeOrphanClose = FakeWebSocket.instances.length;
    first.drop();
    vi.advanceTimersByTime(120_000);
    expect(FakeWebSocket.instances).toHaveLength(beforeOrphanClose);
  });

  /**
   * disconnect() during a backoff window used to be ignored: the pending timer still fired, and
   * connect() clears the "closed" flag, so a session the caller had deliberately ended came
   * back to life.
   */
  it('disconnect during the reconnect backoff stops the retry', () => {
    const client = new GameClient();
    client.connect();
    latest().open();
    latest().drop();

    const afterDrop = FakeWebSocket.instances.length;
    client.disconnect();
    vi.advanceTimersByTime(120_000);

    expect(FakeWebSocket.instances).toHaveLength(afterDrop);
  });

  it('disconnect stops further reconnects after a later drop attempt', () => {
    const client = new GameClient();
    client.connect();
    latest().open();

    client.disconnect();
    vi.advanceTimersByTime(120_000);

    expect(FakeWebSocket.instances).toHaveLength(1);
  });
});

describe('GameClient action queueing', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    FakeWebSocket.instances = [];
    (globalThis as unknown as { WebSocket: unknown }).WebSocket = FakeWebSocket;
    (globalThis as unknown as { window: unknown }).window = { location: { host: 'localhost:19421' } };
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('holds actions until the first state arrives, then flushes them in order', () => {
    const client = new GameClient();
    client.connect();
    const socket = latest();
    socket.open();

    client.sendAction({ type: 'move' } as never);
    client.sendAction({ type: 'turn' } as never);
    expect(socket.sentTypes()).not.toContain('action');

    socket.receive(HELLO); // client answers with "ready"
    socket.receive(STATE); // first state releases the queue

    const actions = socket.sent
      .map((raw) => JSON.parse(raw))
      .filter((envelope) => envelope.type === 'action');
    expect(actions.map((a) => a.payload.type)).toEqual(['move', 'turn']);
  });

  it('re-queues an action rather than dropping it when the socket is gone', () => {
    const client = new GameClient();
    client.connect();
    const socket = latest();
    socket.open();
    socket.receive(HELLO);
    socket.receive(STATE);

    // Socket dies without onclose having run yet — the client still believes it is ready.
    socket.readyState = FakeWebSocket.CLOSED;
    const sentBefore = socket.sent.length;
    client.sendAction({ type: 'move' } as never);

    expect(socket.sent).toHaveLength(sentBefore);

    // Once a fresh connection reaches state, the held action goes out.
    socket.readyState = FakeWebSocket.OPEN;
    socket.receive(STATE);
    // isReady was already true, so flush happens on the next queued send path.
    client.sendAction({ type: 'turn' } as never);

    const actions = socket.sent
      .map((raw) => JSON.parse(raw))
      .filter((envelope) => envelope.type === 'action');
    expect(actions.map((a) => a.payload.type)).toContain('turn');
  });

  /**
   * onclose drops the queue precisely so stale input is not replayed. Input made *after* that,
   * while the client is visibly down and backing off, was still being queued — and a reconnect
   * flushed the lot, so a player who kept pressing forward for the length of a 30s backoff
   * teleported once the session came back.
   */
  it('drops actions taken while disconnected instead of replaying them on reconnect', () => {
    const client = new GameClient();
    client.connect();
    const first = latest();
    first.open();
    first.receive(HELLO);
    first.receive(STATE);

    first.drop();
    client.sendAction({ type: 'move_forward' } as never);
    client.sendAction({ type: 'move_forward' } as never);

    vi.advanceTimersByTime(60_000);
    const second = latest();
    expect(second).not.toBe(first);
    second.open();
    second.receive(HELLO);
    second.receive(STATE);

    expect(second.sentTypes()).not.toContain('action');
  });

  /**
   * A held action must not be overtaken. The queue used to be bypassed entirely whenever the
   * direct send happened to succeed, so a later action went out ahead of an earlier one and the
   * earlier one sat in the queue until some future ready transition — or forever.
   */
  it('keeps a later action behind one already queued', () => {
    const client = new GameClient();
    client.connect();
    const socket = latest();
    socket.open();
    socket.receive(HELLO);
    socket.receive(STATE);

    // The socket dies without onclose running: the client still believes it is ready, so this
    // action is held rather than lost.
    socket.readyState = FakeWebSocket.CLOSED;
    client.sendAction({ type: 'move_forward' } as never);

    // It comes back before onclose fires. The next action must not jump the queue.
    socket.readyState = FakeWebSocket.OPEN;
    client.sendAction({ type: 'turn_left' } as never);

    const actions = socket.sent
      .map((raw) => JSON.parse(raw))
      .filter((envelope) => envelope.type === 'action');
    expect(actions.map((a) => a.payload.type)).toEqual(['move_forward', 'turn_left']);
  });

  /**
   * The handshake window is short, but nothing bounded it: a server that accepts the socket and
   * then never sends state let held input grow without limit for as long as the player kept
   * pressing keys.
   */
  it('bounds the held queue instead of growing without limit', () => {
    const client = new GameClient();
    client.connect();
    const socket = latest();
    socket.open(); // open, but no state yet — everything is held

    for (let i = 0; i < 5000; i++) {
      client.sendAction({ type: 'move_forward' } as never);
    }

    socket.receive(HELLO);
    socket.receive(STATE);

    const actions = socket.sent
      .map((raw) => JSON.parse(raw))
      .filter((envelope) => envelope.type === 'action');
    expect(actions.length).toBeGreaterThan(0);
    expect(actions.length).toBeLessThanOrEqual(256);
  });

  it('answers a heartbeat ping with the matching pong sequence', () => {
    const client = new GameClient();
    client.connect();
    const socket = latest();
    socket.open();
    socket.receive({ v: 2, type: 'heartbeat.ping', seq: 5, payload: { pingSeq: 42 } });

    const pong = socket.sent
      .map((raw) => JSON.parse(raw))
      .find((envelope) => envelope.type === 'heartbeat.pong');
    expect(pong?.payload).toEqual({ pingSeq: 42 });
  });
});

describe('GameClient socket URL', () => {
  beforeEach(() => {
    FakeWebSocket.instances = [];
    (globalThis as unknown as { WebSocket: unknown }).WebSocket = FakeWebSocket;
  });

  function connectFrom(location: { host: string; protocol: string }): string {
    (globalThis as unknown as { window: unknown }).window = { location };
    new GameClient().connect();
    return latest().url;
  }

  it('connects over ws from a plain-http page', () => {
    expect(connectFrom({ host: 'localhost:19421', protocol: 'http:' })).toBe('ws://localhost:19421/ws');
  });

  /**
   * A secure page may not open an insecure socket — the browser blocks it outright, so a
   * hard-coded ws:// scheme means the game never connects at all rather than degrading.
   */
  it('connects over wss from an https page', () => {
    expect(connectFrom({ host: 'reach.example:443', protocol: 'https:' })).toBe('wss://reach.example:443/ws');
  });
});
