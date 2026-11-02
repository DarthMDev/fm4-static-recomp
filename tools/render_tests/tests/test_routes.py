import tempfile
import unittest
from pathlib import Path

from render_tests import routes
from render_tests.game import RunBroken, Session, String

from render_tests.tests.test_game import FakeHost


class ScriptedHost(FakeHost):
    """Answers each press with the next log text scripted for its control (None: dropped)."""

    def __init__(self, log_path, answers):
        super().__init__(log_path)
        self.answers = {control: list(texts) for control, texts in answers.items()}
        self.pressed = []

    def append_input(self, line):
        super().append_input(line)
        control = line.split()[-1]
        self.pressed.append(control)
        queue = self.answers.get(control)
        text = queue.pop(0) if queue else None
        if text:
            self.log(text)

    def log(self, text):
        with open(self.log_path, "a", encoding="utf-8") as log:
            log.write(f"[2026-10-24 01:00:00.000] [info] [core] [t1] {text}\n")


class EventChoiceTest(unittest.TestCase):
    def choose(self, **options):
        calls = []
        session = type("Session", (), {"press_until": lambda *_, **__: None})()
        original = routes.enter, routes._abarth_500
        routes.enter = lambda session, moves, expected, back_to, step: calls.append(moves)
        routes._abarth_500 = lambda session, step: None
        try:
            routes.showroom(session, **options)
        finally:
            routes.enter, routes._abarth_500 = original
        return calls[1]

    def test_quick_race_is_the_first_event(self):
        self.assertEqual(self.choose(), ["LEFT"] * 3)

    def test_hot_lap_is_two_to_the_right(self):
        self.assertEqual(self.choose(hot_lap=True), ["LEFT"] * 3 + ["RIGHT"] * 2)


class HotLapAloneTest(unittest.TestCase):
    def race(self, seen_ai):
        session = type("Session", (), {
            "poll": lambda self: None, "lines": [], "press": lambda *_: None,
            "press_until": lambda *_, **__: None, "wait": lambda *_, **__: None})()
        original = routes.showroom, routes._moves, routes._seen, routes.time.sleep
        routes.showroom = lambda session, hot_lap: None
        routes._moves = lambda session, moves: None
        routes._seen = lambda session, signal, since: signal is routes.AI_DRIVER and seen_ai
        routes.time.sleep = lambda seconds: None
        try:
            routes.quick_race(session, hot_lap=True)
        finally:
            routes.showroom, routes._moves, routes._seen, routes.time.sleep = original

    def test_a_track_without_ai_drivers_is_a_hot_lap(self):
        self.race(seen_ai=False)

    def test_ai_drivers_break_the_run(self):
        with self.assertRaises(RunBroken):
            self.race(seen_ai=True)


class EnterTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.run = Path(self.temp.name) / "run"
        self.log = self.run / "state" / "logs" / "runtime.log"

    def tearDown(self):
        self.temp.cleanup()

    def enter(self, answers, on_screen=("String: AUTOVISTA", "String: FREE PLAY")):
        host = ScriptedHost(self.log, answers)
        for text in on_screen:
            host.log(text)
        with Session(self.run, [], host=host) as session:
            session.poll()
            routes.enter(session, [], String("QUICK RACE"), routes.MAIN_MENU, "free play")
        return host.pressed

    def test_a_dropped_press_is_pressed_again_without_backing_out(self):
        pressed = self.enter({"A": [None, "String: QUICK RACE"]})
        self.assertEqual(pressed, ["A", "A"])

    def test_a_wrong_screen_is_backed_out_of_then_tried_again(self):
        pressed = self.enter({"A": ["String: UPGRADE", "String: QUICK RACE"],
                              "B": ["String: AUTOVISTA"]})
        self.assertEqual(pressed, ["A", "B", "A"])

    def test_the_screens_own_strings_again_are_not_a_new_screen(self):
        pressed = self.enter({"A": ["String: FREE PLAY", "String: QUICK RACE"]})
        self.assertEqual(pressed, ["A", "A"])


if __name__ == "__main__":
    unittest.main()
