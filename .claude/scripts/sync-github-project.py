# -*- coding: utf-8 -*-
"""
plan.md WBS 트리 → GitHub Project(v2) 단방향 동기화.

- plan.md §전체 범위의 노드(x.y)별 상태 마커(✅/🔄/⬜)를 읽어
  GitHub Project의 Status 필드 + 이슈 open/close 를 맞춘다.
- plan.md에 있는데 이슈가 없는 노드(x.y) → 이슈 생성 + Project 추가 + 필드 설정.
- 이슈가 있는데 plan.md에 없는 노드 → 보고만(자동 삭제 안 함).

진실원 = plan.md. 이 스크립트는 전파만 한다(역방향 없음).
post-commit 훅에서 호출(백그라운드). 수동: python .claude/scripts/sync-github-project.py [--dry-run]
"""
import subprocess, json, sys, re, os

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

# ── 설정 (GitHub Project "Multiplay ActionRPG Roadmap") ──────────────
GH = os.environ.get("GH_BIN", r"C:\Program Files\GitHub CLI\gh.exe")
OWNER = "SeoBYP"
REPO = "SeoBYP/Multiplay-ActionRPG-Unity"
PN = "2"
PROJECT_ID = "PVT_kwHOBmgmRc4BZn-_"
STATUS_FIELD = "PVTSSF_lAHOBmgmRc4BZn-_zhUlV-k"
STATUS_OPT = {"Todo": "f75ad846", "In Progress": "47fc9ee4", "Done": "98236657"}
TIER_FIELD = "PVTSSF_lAHOBmgmRc4BZn-_zhUlWB4"
TIER_OPT = {"T1": "3c9f8a46", "T2": "e8e77443", "T3": "fbbdec43"}
OWNER_FIELD = "PVTSSF_lAHOBmgmRc4BZn-_zhUlWB8"
OWNER_OPT = {"server": "bb27657b", "gas": "5f3c2ef7", "anim": "a0a1c8ac", "unassigned": "7afbae5a"}

EXCLUDE = {"9.2"}  # 9.2 DungeonId == 4.3 (이슈 단일화)
EMOJI_STATUS = {"✅": "Done", "🔄": "In Progress", "⬜": "Todo"}
EMOJI_OWNER = {"🟢": "server", "🔵": "gas", "🟣": "anim", "⚪": "unassigned"}
AREA_BY_GROUP = {"2": "character", "3": "item", "4": "content", "5": "social",
                 "6": "meta", "7": "ui", "8": "audio", "9": "infra"}

HERE = os.path.dirname(os.path.abspath(__file__))
PLAN = os.path.normpath(os.path.join(HERE, "..", "..", "docs", "wiki", "plan.md"))
DRY = "--dry-run" in sys.argv

NODE_RE = re.compile(r"^\s*-\s+\*\*(\d+\.\d+)(?=[\s*])(.*)$")


def gh(args):
    r = subprocess.run([GH] + args, capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return r.returncode, (r.stdout or "").strip(), (r.stderr or "").strip()


def parse_plan():
    """plan.md §전체 범위에서 노드 x.y → {status,tier,owner,area,name} 추출."""
    nodes = {}
    in_wbs = False
    with open(PLAN, encoding="utf-8") as f:
        for line in f:
            if line.startswith("## "):
                in_wbs = "전체 범위" in line
                continue
            if not in_wbs:
                continue
            m = NODE_RE.match(line)
            if not m:
                continue
            nid, rest = m.group(1), m.group(2)
            if nid.split(".")[0] == "1":
                continue  # 그룹 1 = 완료 기반(이슈 대상 아님)
            status = next((s for e, s in EMOJI_STATUS.items() if e in line), None)
            if status is None:
                continue  # 상태 마커 없는 줄(상위 그룹 등)은 건너뜀
            owner = next((o for e, o in EMOJI_OWNER.items() if e in line), "unassigned")
            tm = re.search(r"\bT([123])\b", line)
            tier = ("T" + tm.group(1)) if tm else None
            name = _extract_name(nid, line)
            nodes[nid] = dict(status=status, tier=tier, owner=owner,
                              area=AREA_BY_GROUP.get(nid.split(".")[0], "infra"),
                              name=name)
    return nodes


def _extract_name(nid, line):
    # `- **2.5 사망/부활** ...` → "사망/부활"  /  `- **2.1** 전투 코어 — ...` → "전투 코어"
    mbold = re.search(r"\*\*" + re.escape(nid) + r"\s+([^*]+)\*\*", line)
    if mbold:
        return mbold.group(1).strip()
    mafter = re.search(r"\*\*" + re.escape(nid) + r"\*\*\s*(.+)$", line)
    if mafter:
        seg = re.split(r"\s+—\s+|\s+\(|\s+`", mafter.group(1).strip())[0]
        return seg.strip()
    return nid


def fetch_issues():
    code, out, _ = gh(["issue", "list", "--repo", REPO, "--state", "all",
                       "--limit", "500", "--json", "number,title,state"])
    res = {}
    if code == 0 and out:
        for i in json.loads(out):
            tok = i["title"].split(" ", 1)[0]
            res[tok] = dict(number=i["number"], title=i["title"], state=i["state"])
    return res


def fetch_items():
    code, out, _ = gh(["project", "item-list", PN, "--owner", OWNER,
                       "--format", "json", "-L", "200"])
    res = {}
    if code == 0 and out:
        for it in json.loads(out)["items"]:
            c = it.get("content") or {}
            if c.get("type") == "Issue":
                res[c["number"]] = dict(item_id=it["id"], status=it.get("status"),
                                        tier=it.get("tier"), owner=it.get("owner"))
    return res


def set_field(item_id, field, opt):
    if DRY:
        return
    gh(["project", "item-edit", "--id", item_id, "--project-id", PROJECT_ID,
        "--field-id", field, "--single-select-option-id", opt])


def main():
    plan = parse_plan()
    issues = fetch_issues()
    items = fetch_items()
    changes, creates, orphans = [], [], []

    for nid, n in sorted(plan.items()):
        if nid in EXCLUDE:
            continue
        iss = issues.get(nid)
        if not iss:
            creates.append(nid)
            if not DRY:
                _create(nid, n)
            continue
        num = iss["number"]
        it = items.get(num)
        want = n["status"]
        # 1) Status 필드
        if it and it.get("status") != want:
            changes.append(f"{nid}: status {it.get('status')} → {want}")
            if it.get("item_id"):
                set_field(it["item_id"], STATUS_FIELD, STATUS_OPT[want])
        # 2) 이슈 open/close (Done=close)
        want_state = "CLOSED" if want == "Done" else "OPEN"
        if iss["state"] != want_state and not DRY:
            gh(["issue", "close" if want_state == "CLOSED" else "reopen",
                str(num), "--repo", REPO])

    plan_nodes = {k for k in plan if k not in EXCLUDE}
    for tok, iss in issues.items():
        if re.match(r"^\d+\.\d+$", tok) and tok not in plan_nodes and tok not in EXCLUDE:
            orphans.append(f"{tok} (#{iss['number']} {iss['title']})")

    print(f"[sync] matched={len(plan_nodes)} statusChanges={len(changes)} "
          f"creates={len(creates)} orphans={len(orphans)} dry={DRY}")
    for c in changes:
        print("  Δ", c)
    for c in creates:
        print("  + create", c, plan[c]["name"])
    for o in orphans:
        print("  ? orphan(보고만)", o)


def _create(nid, n):
    title = f"{nid} {n['name']}"
    args = ["issue", "create", "--repo", REPO, "--title", title,
            "--body", f"**WBS {nid}** · {n['tier'] or '기술부채'} · owner: {n['owner']}\n\n"
                      f"— 출처: `docs/wiki/plan.md` §전체 범위 (자동 생성)",
            "--label", f"area:{n['area']}", "--label", f"owner:{n['owner']}"]
    if n["tier"]:
        args += ["--label", f"tier:{n['tier']}"]
    code, out, err = gh(args)
    if code != 0 or "github.com" not in out:
        print("  ! create FAIL", nid, err or out)
        return
    url = out.splitlines()[-1].strip()
    code, out2, _ = gh(["project", "item-add", PN, "--owner", OWNER, "--url", url, "--format", "json"])
    try:
        iid = json.loads(out2)["id"]
    except Exception:
        return
    set_field(iid, STATUS_FIELD, STATUS_OPT[n["status"]])
    set_field(iid, OWNER_FIELD, OWNER_OPT[n["owner"]])
    if n["tier"]:
        set_field(iid, TIER_FIELD, TIER_OPT[n["tier"]])


if __name__ == "__main__":
    main()
