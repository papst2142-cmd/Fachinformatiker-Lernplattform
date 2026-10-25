# -*- coding: utf-8 -*-
"""Spielwerte-Fingerabdruck: spielt den spaeten Demo-Stand (szenario045.spaet + 8
Tage) durch und hasht Ereignisprotokoll und Spielstand-Werte (ohne Texte).
python3.12 mit_home.py fingerabdruck.py <repo>"""
import hashlib, json, os, sys, tempfile
REPO = sys.argv[1]
sys.path.insert(0, REPO)
sys.path.insert(1, os.path.dirname(os.path.abspath(__file__)))
import fisi_core as core
import fisi_game as fg
import szenario045 as sz

db = core.DBManager(os.path.join(tempfile.mkdtemp(), "f.db"))
game = fg.Game(db, "PC")
sz.spaet(game, branch=True)
for room in ("lager", "besprechung", "serverraum"):
    game.build_room(room)
game.book_ad("online", fg.AD_RUNNING)
game.start_cert("iso9001")
game.take_loan(15000, 100, fg.FREE_LOAN)
for _ in range(8):
    sz.play_day(game)
game.reload()
s = game.state
events = [(kind, data) for _ts, kind, data in s.history]
values = {
    "tag": s.day, "geld": round(s.money, 6), "ansehen": s.reputation if hasattr(s, "reputation") else None,
    "mitarbeiter": sorted((m["id"], m.get("gehalt"), json.dumps(m.get("werte"), sort_keys=True))
                          for m in s.staff_list()),
    "stimmung": sorted(s.mood.items()) if hasattr(s, "mood") else None,
}
blob = json.dumps({"events": events, "werte": values}, sort_keys=True, default=str,
                  ensure_ascii=False)
print("Ereignisse", len(events), "Tag", s.day, "Geld", round(s.money, 2))
print("Fingerabdruck", hashlib.sha256(blob.encode("utf-8")).hexdigest())
