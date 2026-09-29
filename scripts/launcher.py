#!/usr/bin/env python3
"""A small window with two buttons: start the website locally, or run all the tests.

Each command opens in its own terminal window, so its output stays visible and it can be stopped with Ctrl+C.

    python3 scripts/launcher.py
"""

import os
import platform
import shlex
import shutil
import subprocess
import tkinter as tk
from pathlib import Path
from tkinter import messagebox

REPO_ROOT = Path(__file__).resolve().parent.parent
BACKEND = REPO_ROOT / "backend"
FRONTEND = REPO_ROOT / "frontend"

# (window title, working directory, command); the same commands as in README.md and docs/testing.md.
WEBSITE_COMMANDS = [
    ("DLF backend", REPO_ROOT, "dotnet run --project backend/src/DlfVoting.Api"),
    ("DLF frontend", FRONTEND, "npm run dev"),
]
TEST_COMMANDS = [
    ("DLF backend tests", REPO_ROOT, "dotnet test backend/tests/DlfVoting.Api.Tests"),
    ("DLF frontend tests", FRONTEND, "npm test"),
]


def _applescript_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def open_terminal(title: str, cwd: Path, command: str) -> None:
    """Opens a new terminal window that runs `command` in `cwd` and stays open afterwards."""
    system = platform.system()

    if system == "Darwin":
        shell_command = f"cd {shlex.quote(str(cwd))} && printf '\\e]0;%s\\a' {shlex.quote(title)} && {command}"
        script = (
            'tell application "Terminal"\n'
            "    activate\n"
            f"    do script {_applescript_string(shell_command)}\n"
            "end tell"
        )
        subprocess.Popen(["osascript", "-e", script])

    elif system == "Windows":
        # "start" needs a shell; /k keeps the window open when the command ends.
        subprocess.Popen(f'start "{title}" cmd /k {command}', cwd=cwd, shell=True)

    else:
        shell_command = f"{command}; exec $SHELL"
        for terminal, args in (
            ("gnome-terminal", ["--title", title, "--", "bash", "-c", shell_command]),
            ("konsole", ["-p", f"tabtitle={title}", "-e", "bash", "-c", shell_command]),
            ("xterm", ["-T", title, "-e", "bash", "-c", shell_command]),
        ):
            if shutil.which(terminal):
                subprocess.Popen([terminal, *args], cwd=cwd)
                return
        raise RuntimeError("No supported terminal found (tried gnome-terminal, konsole and xterm).")


def run_all(commands: list[tuple[str, Path, str]], status: tk.StringVar, done_message: str) -> None:
    try:
        for title, cwd, command in commands:
            open_terminal(title, cwd, command)
    except (OSError, RuntimeError) as error:
        messagebox.showerror("Could not open a terminal", str(error))
        return
    status.set(done_message)


def main() -> None:
    root = tk.Tk()
    root.title("DLF Voting")
    root.resizable(False, False)

    frame = tk.Frame(root, padx=24, pady=20)
    frame.pack()

    tk.Label(frame, text="DLF Voting", font=("TkDefaultFont", 16, "bold")).pack(pady=(0, 12))

    status = tk.StringVar(value="Each button opens new terminal windows.")

    tk.Button(
        frame,
        text="Start website",
        width=22,
        command=lambda: run_all(
            WEBSITE_COMMANDS, status, "Starting backend and frontend: open http://localhost:5173 when ready."
        ),
    ).pack(pady=4)

    tk.Button(
        frame,
        text="Run tests",
        width=22,
        command=lambda: run_all(TEST_COMMANDS, status, "Running backend and frontend tests."),
    ).pack(pady=4)

    tk.Label(frame, textvariable=status, wraplength=260, justify="center", fg="gray40").pack(pady=(12, 0))

    root.mainloop()


if __name__ == "__main__":
    if not BACKEND.is_dir() or not FRONTEND.is_dir():
        raise SystemExit(f"Expected backend/ and frontend/ in {REPO_ROOT}")
    os.chdir(REPO_ROOT)
    main()
