"""Resolve the developer's explicitly selected local game copy."""
import os
from pathlib import Path


def game_directory():
    value = os.environ.get('STONESHARD_DIR')
    if not value:
        raise SystemExit('Set STONESHARD_DIR to your local Stoneshard installation directory.')
    path = Path(value).expanduser().resolve()
    if not (path / 'StoneShard.exe').is_file():
        raise SystemExit('STONESHARD_DIR must contain StoneShard.exe.')
    return path
