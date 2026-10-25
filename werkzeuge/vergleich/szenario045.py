# -*- coding: utf-8 -*-
"""Spielstaende fuer die Screenshots 0.45 (PC und Handy).
frueh(game): Tag 12, noch angestellt bei Bitweiche.
spaet(game): eigene Firma auf Stufe 4 mit 7 Mitarbeitern, 40+ Firmentage,
Filiale eroeffnet und ein Mitarbeiter versetzt."""
import fisi_game as fg

LOOK = {"haut": "hell", "frisur": "kurz", "haarfarbe": "braun", "oberteil": "cyan", "kreis": "cyan"}


def frueh(game, days=12):
    game.set_profile("Nico", LOOK)
    for i, task in enumerate(fg.GAME["aufgaben"][:30]):
        game._log(fg.EV_SOLVED, {"aufgabe": task["id"], "tag": min(days, 1 + i // 3),
                                 "richtig": True, "geld": 60,
                                 "reputation": {k: 1 for k in fg.AXIS_KEYS}})
    for day in range(1, days):
        game._log(fg.EV_DAY_END, {"tag": day, "gehalt": 140})
    game.reload()


def play_day(game):
    state = game.state
    for item in fg.applicants(state):
        if len(state.staff) < min(7, state.capacity) and item.get("herkunft") != "bitweiche":
            game.hire(item["id"]); state = game.state
    for decision in state.open_decisions():
        game.decide(decision["id"], decision["optionen"][0]["id"])
    state = game.state
    for inquiry in state.inquiries():
        if not inquiry.get("ergebnis"):
            game.send_offer(inquiry["id"], 15, fg.find_solution(fg.inquiry_task(inquiry, 15)))
    for ticket in game.state.customer_tickets():
        if ticket.get("an"):
            continue
        state = game.state
        options = [o for o in fg.ticket_candidates(state, ticket, game.firm_levels())
                   if not o["problem"] and o["an"] != fg.SELF]
        if options:
            game.delegate(ticket["id"], max(options, key=lambda o: o["chance"])["an"])
    return game.end_day()


def spaet(game, branch=True):
    game.set_profile("Nico", LOOK)
    for i, task in enumerate(fg.GAME["aufgaben"]):
        game._log(fg.EV_SOLVED, {"aufgabe": task["id"], "tag": min(113, 1 + i // 4),
                                 "richtig": True, "geld": 60,
                                 "reputation": {k: 1 for k in fg.AXIS_KEYS}})
    for day in range(1, 115):
        game._log(fg.EV_DAY_END, {"tag": day, "gehalt": 250 if day > 60 else 140, "rang": "Senior"})
    game._log(fg.EV_SOLVED, {"aufgabe": "probe-erspartes", "tag": 114, "richtig": False,
                             "geld": 400000})
    game.reload()
    game.found_firm("Nico IT-Service")
    for _ in range(3):
        game.expand()
    original = fg.personal_events
    fg.personal_events = lambda state, day, content=None: []
    try:
        while game.state.day < 160:
            play_day(game)
    finally:
        fg.personal_events = original
    game.reload()
    if branch and fg.branch_possible(game.state):
        game.open_branch("Filiale Lindenau")
        staff = [item for item in game.state.staff_list() if item["standort"] == fg.SITE_OFFICE]
        if staff:
            game.transfer(staff[-1]["id"], fg.SITE_BRANCH)
    game.reload()
