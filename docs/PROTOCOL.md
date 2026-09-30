# Odyssey session protocol

The plugin speaks JSON text frames over a WebSocket. One object per frame. Property names are camel case. Enums are camel case strings (`fate`, `ares`, `strength`). Times are UTC.

Leave the server URL empty and the plugin keeps a local session instead. That loopback is not shared and is not a database.

The server should follow the same rules as `SessionHost` in `PNX.Odyssey.Core`. Identity is the character name plus home world, taken from the `hello` frame and then bound to that socket. Later frames do not get to change it.

## Connection

Client sends `hello` immediately after the socket opens:

```json
{ "type": "hello", "name": "Furia Bloom", "world": "Phoenix", "pluginKey": "optional" }
```

Server replies with `welcome`. `session` is null until they join. `sessions` is every open session.

```json
{
  "type": "welcome",
  "selfId": "furia bloom@phoenix",
  "sessions": [],
  "session": null
}
```

The client then sends `heartbeat` about every 10 seconds. A member who is not seen for 25 seconds is no longer live. God roles are unique among connected members. Fate is not unique. A reconnecting god loses the role if someone else holds it.

`state` and `rejected` always include the current `session` (`null` if they are not in one) and `sessions`.

## Session

`session.create` carries `sessionName` and `password`. The password only closes the session. Store a hash, not the password. Joining is open.

`session.join` carries `sessionId`.

`session.leave` drops the caller out. Registered players stay.

`session.close` carries `sessionId` and `password`. A mismatch returns `rejected`. Success removes the session for everyone.

A summary is `{ id, name, createdAt, liveCount }`. `liveCount` is connected plugin users, not the registered roster.

## Roles and registration

`role.set` carries `role`. Gods are unique. Fate is shared.

`participant.register` carries `firstName`, `lastName`, `world`, `discord`, `threads`, `level`. The caller must already have a role. Starting threads are 1 through 10, default 4. The same name and world cannot be registered twice.

`participant.level` updates the in-game level and does not bump the participant revision.

`threads.set` is Fate only. `threads` is 0 through 10 and `revision` must match. 0 threads is a Shade. The offering price is 25,000 gil per added thread. The plugin calculates it; the server stores the count.

`participant.complete` is Fate only, and only once Strength, Harmony, Fear, and Power all have a victor. It clears those trials, keeps the thread count, and adds 1 to the run. The first run is 1.

`participant.remove` carries `participantId` and the session close password. A mismatch is rejected. The player, their dice, and their edit locks are removed.

## Trials

The caller's role decides the trial. Ares and Athena are Strength. Hera and Aphrodite are Harmony. Hades and Poseidon are Fear. Zeus is Power.

Mortals roll 3d6. Gods roll 3d6, except Zeus, who rolls 3d8. Pass and Fail stay manual.

- Strength and Power: higher total wins.
- Harmony: the total closer to 11 wins.
- Fear: more odd faces wins.
- One god from a pair clears that trial. The first victor stays.
- Zeus stays closed until the other three trials are cleared.
- A Shade cannot roll, pass, or fail. Fail spends one thread, clears the current dice, and does not undo a victor. Pass records the caller and clears the dice.

`trial.dice` carries `participantId`, `side` (`player` or `god`), `values` (the full history for that side), and `revision`. The god who holds the trial lock is the reporter. A history that extends the current one is accepted when the revision is slightly behind, so a fast pair of rolls is not lost. A conflicting replace is `rejected` with `savedBy`.

Pass and Fail do not require dice. A loss still spends one thread.

## Edit highlight

`edit.begin` and `edit.end` carry `scope`:

- `threads:{participantId}`
- `register:{participantId}`
- `trial:{participantId}`

A scope stays with the first holder until `edit.end` or 20 seconds without a refresh. The plugin refreshes every 5 seconds while that editor is open. Other clients highlight the row. They are not the ones who may apply it. A write with an old `revision` is rejected:

```json
{
  "type": "rejected",
  "reason": "Furia Bloom saved first.",
  "savedBy": "Furia Bloom",
  "sessions": [],
  "session": {}
}
```
