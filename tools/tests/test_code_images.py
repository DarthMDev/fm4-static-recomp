import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import code_images as ci  # noqa: E402
import release_analysis  # noqa: E402


class FakeXex:
    def __init__(self, base, words):
        self.base = base
        self.data = b"".join(w.to_bytes(4, "big") for w in words)
        self.pdata = []

    def read(self, address, size):
        offset = address - self.base
        return self.data[offset:offset + size]


class NormaliseTest(unittest.TestCase):
    def test_an_address_built_by_lis_is_masked(self):
        # lis r11, 0x82DC; addi r3, r11, 0x3750
        xex = FakeXex(0x82000000, [0x3D6082DC, 0x386B3750])
        self.assertEqual(ci.normalise(xex, 0x82000000, 8), [0x3D600000, 0x386B0000])

    def test_a_field_of_a_loaded_pointer_is_kept(self):
        # lis r11, 0x82DC; lwz r11, 0x3750(r11); lwz r3, 0x40(r11): 0x40 is a struct offset,
        # which a changed class layout moves.
        xex = FakeXex(0x82000000, [0x3D6082DC, 0x816B3750, 0x806B0040])
        self.assertEqual(ci.normalise(xex, 0x82000000, 12), [0x3D600000, 0x816B0000, 0x806B0040])

    def test_a_store_keeps_its_base_an_address(self):
        # lis r11, 0x82DC; stw r3, 0x3750(r11); stw r4, 0x3754(r11)
        xex = FakeXex(0x82000000, [0x3D6082DC, 0x906B3750, 0x908B3754])
        self.assertEqual(ci.normalise(xex, 0x82000000, 12), [0x3D600000, 0x906B0000, 0x908B0000])


class ResolvedAnalysisTest(unittest.TestCase):
    def test_hex_addresses_move(self):
        text = "setjmp_address = 0x82762EC0  # 0x82762EC0 stays in the comment\n"
        resolved = release_analysis.rewrite_addresses(text, {0x82762EC0: 0x82768B30})
        self.assertEqual(resolved, "setjmp_address = 0x82768B30  # 0x82762EC0 stays in the comment\n")

    def test_an_address_the_rewrite_cannot_see_stops_it(self):
        # The same address in decimal: collected as an address, but no hex literal to move.
        text = "setjmp_address = 2188783296\n"
        with self.assertRaises(SystemExit):
            release_analysis.rewrite_addresses(text, {0x82762EC0: 0x82768B30})

    def test_an_address_without_a_port_stops_it(self):
        with self.assertRaises(SystemExit):
            release_analysis.rewrite_addresses("longjmp_address = 0x82762A90\n", {})


class ModReleaseTest(unittest.TestCase):
    def test_the_mod_xex_is_a_1_0_code_source(self):
        mods = [r for r in ci.releases() if r.get("mod")]
        self.assertEqual(len(mods), 1)
        self.assertEqual({k: v for k, v in mods[0].items() if k != "files"},
                         {"name": "Project Forza Plus 4 1.3", "media_id": "633772A0", "code_image": "1.0", "mod": True})
        sha = "FEA11E87FF8434E51ED8E683023EA17281DEC02A5ED8FA226B1C73726CF15128"
        self.assertEqual(mods[0]["files"], [{"path": "default.xex", "size": 16068608, "sha256": sha}])
        self.assertIn(sha, ci.executable_hashes("1.0"))

    def test_regenerating_the_disc_manifest_carries_the_mod_release(self):
        spec = importlib.util.spec_from_file_location("make_disc_manifest", ci.ROOT / "tools/make-disc-manifest.py")
        tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(tool)
        with tempfile.TemporaryDirectory() as temp:
            temp = Path(temp)
            (temp / "disc2").mkdir()
            out = temp / "out.json"
            argv = sys.argv
            sys.argv = ["make-disc-manifest.py", "--base", str(ci.DISCS), str(temp / "disc2"), str(out)]
            try:
                tool.main()
            finally:
                sys.argv = argv
            written = json.loads(out.read_text(encoding="utf-8"))["disc1"]
        self.assertEqual(written["releases"], json.loads(ci.DISCS.read_text(encoding="utf-8"))["disc1"]["releases"])


if __name__ == "__main__":
    unittest.main()
