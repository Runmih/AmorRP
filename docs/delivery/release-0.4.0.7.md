# Amor RP 0.4.0.7 — Dockable quick bar

The quick bar docks to any viewport edge and expands inward when you hover over
its handle. The top and bottom layouts are horizontal. The left and right layouts
are vertical. The default is auto-hide at the middle of the left edge.

Settings → Interface saves the edge, position, size, opacity and auto-hide
preference. You can also drag the handle to reposition the bar. Click the handle
to pin or unpin it temporarily, or disable Auto-hide to keep it visible.

The server floor remains 0.4.0.3. In game, verify reveal and drag on all edges,
pointer movement from the handle to the buttons, opacity, and placement after
changing viewport size. The layout tests cover the dock geometry independently
of the game.

The stable feed uses the [Windows package from CI](https://github.com/Runmih/AmorRP/actions/runs/44786901444).
The matching [server workflow](https://github.com/Runmih/AmorRP/actions/runs/44786901345)
also passed. The Windows package SHA-256 is
`222921e210fd7b4cf49201ba2d8d55e5ff7790a323498dd89d8a09e01002e02e`.
