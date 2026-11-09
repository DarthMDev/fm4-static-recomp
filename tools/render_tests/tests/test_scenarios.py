# Copyright (c) 2026, i2kiselev
# BSD 3-Clause License: see LICENSE in the repository root.
import unittest

from render_tests import scenarios


class CounterToleranceTest(unittest.TestCase):
    def test_project_forza_plus_4_gets_its_own_tolerance(self):
        self.assertEqual(scenarios.counter_tolerance("Project Forza Plus 4 1.3"),
                         scenarios.PFP4_COUNTER_TOLERANCE)

    def test_the_stock_releases_keep_the_tight_one(self):
        for name in ("1.0 PAL (Europe, Australia)", "1.0 USA", "Rev 1 Europe", "Rev 1 USA"):
            self.assertEqual(scenarios.counter_tolerance(name), scenarios.COUNTER_TOLERANCE)


if __name__ == "__main__":
    unittest.main()
