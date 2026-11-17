#!/usr/bin/env python3
"""Automated playtester for Knot & Spool on an Android device.

The game logs its settled state as `KS_STATE {json}` lines (see GameController.LogState). This script reads
them from `adb logcat`, picks a spool with a simple heuristic, taps it with `adb shell input tap`, and keeps
going for a number of levels. Screenshots of each finished level are saved to the output folder.

Usage: tools/playtest.py [--levels 5] [--out playtest-out] [--adb ~/Android/Sdk/platform-tools/adb]
"""
import argparse
import json
import os
import subprocess
import sys
import threading
import time

PACKAGE = "com.elkuku.knotandspool"
ACTIVITY = PACKAGE + "/com.unity3d.player.UnityPlayerGameActivity"


class Device:
    def __init__(self, adb):
        self.adb = adb
        self.latest = None
        self.latest_at = 0.0
        self.lock = threading.Lock()

    def run(self, *args):
        return subprocess.run([self.adb, *args], capture_output=True, text=True)

    def start_log_reader(self):
        self.run("logcat", "-c")
        proc = subprocess.Popen([self.adb, "logcat", "-s", "Unity:I", "-v", "raw"],
                                stdout=subprocess.PIPE, text=True, errors="replace")

        def reader():
            for line in proc.stdout:
                i = line.find("KS_STATE ")
                if i < 0:
                    continue
                try:
                    state = json.loads(line[i + 9:])
                except json.JSONDecodeError:
                    continue
                with self.lock:
                    self.latest = state
                    self.latest_at = time.time()

        threading.Thread(target=reader, daemon=True).start()
        return proc

    def settled_state(self, quiet=0.7, timeout=20.0):
        """Latest state once no new state has arrived for `quiet` seconds."""
        end = time.time() + timeout
        while time.time() < end:
            with self.lock:
                state, at = self.latest, self.latest_at
            if state is not None and time.time() - at >= quiet and not state.get("busy", 0):
                return state
            time.sleep(0.1)
        with self.lock:
            return self.latest

    def tap(self, x, y, state):
        sw, sh = state["screen"]
        dw, dh = self.size
        self.run("shell", "input", "swipe", str(int(x * dw / sw)), str(int(y * dh / sh)),
                 str(int(x * dw / sw)), str(int(y * dh / sh)), "60")

    def screenshot(self, path):
        with open(path, "wb") as f:
            subprocess.run([self.adb, "exec-out", "screencap", "-p"], stdout=f)

    def read_size(self):
        out = self.run("shell", "wm", "size").stdout.strip().split()[-1]
        w, h = out.split("x")
        self.size = (int(w), int(h))


def choose_spool(state):
    """Pick the reachable spool that is most useful right now, or None to wait."""
    columns = state["columns"]
    tray = state["tray"]
    free = sum(1 for s in tray if s is None)
    if free == 0:
        return None

    bottoms = [col[0] for col in columns if col]
    near = [c for col in columns for c in col[:3]]
    tray_need = {}
    for s in tray:
        if s:
            tray_need[s[0]] = tray_need.get(s[0], 0) + s[1]

    best, best_score = None, None
    for spool in state["board"]:
        color, count, reach, x, y = spool[:5]
        if not reach or color < 0:
            continue
        open_now = bottoms.count(color)
        soon = near.count(color)
        # Prefer colors that can be wound right away and that aren't already covered by the tray.
        score = open_now * 4 + soon - tray_need.get(color, 0) * 2
        if open_now == 0 and free <= 2:
            score -= 50  # don't clog the tray with spools that can't do anything yet
        if best_score is None or score > best_score:
            best, best_score = spool, score
    if best is None:
        return None
    tray_busy = any(s and s[0] in bottoms for s in tray)
    if best_score < -40 and tray_busy:
        return None  # better to wait for the tray to work
    return best


# ---------- solver (mirrors LevelGenerator.PickColumn / Reachable and the game's winding) ----------

def pick_column(ncols, nslots, slot, color, bottoms):
    best, best_d = -1, None
    for c in range(ncols):
        if bottoms[c] != color:
            continue
        d = abs((2 * c + 1) * nslots - (2 * slot + 1) * ncols)  # exact, ties -> leftmost (as in C#)
        if best_d is None or d < best_d:
            best, best_d = c, d
    return best


def wind(columns, pos, tray):
    """Winds until nothing changes (round-robin over slots, one square each). Mutates pos and tray."""
    ncols = len(columns)
    progress = True
    while progress:
        progress = False
        for s in range(len(tray)):
            if tray[s] is None:
                continue
            bottoms = [columns[c][pos[c]] if pos[c] < len(columns[c]) else -1 for c in range(ncols)]
            col = pick_column(ncols, len(tray), s, tray[s][0], bottoms)
            if col < 0:
                continue
            pos[col] += 1
            progress = True
            color, need = tray[s]
            tray[s] = None if need == 1 else (color, need - 1)


def reachable_cells(bw, bh, walls, occupied):
    open_ = set()
    queue = [(r, 0) for r in []]
    for c in range(bw):
        if (0, c) not in walls and (0, c) not in occupied:
            open_.add((0, c))
            queue.append((0, c))
    while queue:
        r, c = queue.pop()
        for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (r + dr, c + dc)
            if 0 <= q[0] < bh and 0 <= q[1] < bw and q not in open_ and q not in walls and q not in occupied:
                open_.add(q)
                queue.append(q)
    reach = set()
    for (r, c) in occupied:
        if r == 0 or any((r + dr, c + dc) in open_ for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            reach.add((r, c))
    return reach


def solve(state, budget=200000):
    """Returns a list of board cells (row, col) to tap, in order, or None. Taps happen when nothing winds."""
    columns = state["columns"]
    bw, bh = state["boardSize"]
    walls = {tuple(w) for w in state["walls"]}
    board = {(b[6], b[7]): (b[5], b[1]) for b in state["board"]}
    tray = [None if s is None else (s[0], s[1]) for s in state["tray"]]
    pos = [0] * len(columns)
    seen = set()
    nodes = [0]

    def rec(pos, tray, board):
        pos, tray = list(pos), list(tray)
        wind(columns, pos, tray)
        if all(pos[c] >= len(columns[c]) for c in range(len(columns))):
            return []
        nodes[0] += 1
        if nodes[0] > budget:
            return None
        if None not in tray:
            return None
        key = (tuple(pos), tuple(tray), frozenset(board))
        if key in seen:
            return None
        seen.add(key)
        free = tray.index(None)
        bottoms = [columns[c][pos[c]] if pos[c] < len(columns[c]) else -1 for c in range(len(columns))]
        reach = reachable_cells(bw, bh, walls, set(board))
        cands = sorted(reach, key=lambda cell: -bottoms.count(board[cell][0]))
        for cell in cands:
            t2 = list(tray)
            t2[free] = board[cell]
            b2 = dict(board)
            del b2[cell]
            rest = rec(pos, t2, b2)
            if rest is not None:
                return [cell] + rest
        return None

    return rec(pos, tray, board)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--levels", type=int, default=5)
    ap.add_argument("--out", default="playtest-out")
    ap.add_argument("--adb", default=os.path.expanduser("~/Android/Sdk/platform-tools/adb"))
    ap.add_argument("--fresh", action="store_true", help="clear app data first (start at level 1)")
    ap.add_argument("--solve", action="store_true", help="play with an exact solver instead of the heuristic")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    dev = Device(args.adb)
    dev.read_size()
    if args.fresh:
        dev.run("shell", "pm", "clear", PACKAGE)
    dev.run("shell", "am", "force-stop", PACKAGE)
    proc = dev.start_log_reader()
    dev.run("shell", "am", "start", "-n", ACTIVITY)

    results = []
    finished = 0
    taps = 0
    level_started = time.time()
    try:
        while finished < args.levels:
            state = dev.settled_state()
            if state is None:
                print("no state received from the game", file=sys.stderr)
                return 1

            if state["overlay"]:
                won = state["state"] == "Won"
                shot = os.path.join(args.out, f"level{state['level']:02d}-{'won' if won else 'lost'}.png")
                dev.screenshot(shot)
                secs = time.time() - level_started
                results.append((state["level"], "WON" if won else "LOST", taps, secs))
                print(f"level {state['level']:2d}: {'WON ' if won else 'LOST'} after {taps} taps, {secs:.0f}s  -> {shot}")
                finished += 1
                taps = 0
                level_started = time.time()
                dev.tap(*state["button"], state)
                time.sleep(1.5)
                continue

            if state["state"] != "Playing":
                time.sleep(0.3)
                continue

            if args.solve:
                plan = solve(state)
                if plan is None:
                    print(f"level {state['level']:2d}: solver found NO solution from this position")
                    spool = choose_spool(state)
                else:
                    if not plan:
                        time.sleep(0.5)
                        continue
                    cell = plan[0]
                    spool = next(b for b in state["board"] if (b[6], b[7]) == cell)
            else:
                spool = choose_spool(state)
            if spool is None:
                time.sleep(0.5)
                # nothing to do: wait for the tray to work, the game decides win/lose
                with dev.lock:
                    dev.latest_at = time.time() - 0.3
                continue

            if taps == 0:
                dev.screenshot(os.path.join(args.out, f"level{state['level']:02d}-start.png"))
            dev.tap(spool[3], spool[4], state)
            taps += 1
            time.sleep(0.4)
    finally:
        proc.terminate()

    print("\nsummary:")
    for level, result, n, secs in results:
        print(f"  level {level:2d}  {result:4s}  {n:3d} taps  {secs:4.0f}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
