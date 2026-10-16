"""Compatibility entry point for the M0 packaging instructions."""

import runpy
import sys
from pathlib import Path

sys.argv = [str(Path(__file__).with_name("package-milestone.py")), "--milestone", "M0"]
runpy.run_path(sys.argv[0], run_name="__main__")
