"""Probe the 5-step rail on Custom Commercial Commission Request.

Reports the chips, how many are marked active, the aria-current values and the
"Step N of 5" caption for a fresh GET and for a partially filled POST.
"""
import http.cookiejar
import re
import sys
import urllib.parse
import urllib.request

BASE = "http://localhost:8095"
OPENER = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
)


def get(path):
    req = urllib.request.Request(BASE + path, headers={"User-Agent": "probe"})
    return OPENER.open(req, timeout=60).read().decode("utf-8", "replace")


def post(path, fields):
    data = urllib.parse.urlencode(fields, doseq=True).encode()
    req = urllib.request.Request(BASE + path, data=data, headers={"User-Agent": "probe"})
    return OPENER.open(req, timeout=60).read().decode("utf-8", "replace")


def csrf(html):
    m = re.search(r'name="__csrf"[^>]*value="([^"]+)"', html) or \
        re.search(r'value="([^"]+)"[^>]*name="__csrf"', html)
    return m.group(1) if m else ""


def report(label, html):
    chips = re.findall(r'<span class="(step[^"]*)"[^>]*aria-current="([^"]+)"', html)
    active = [c for c in chips if " on" in c[0] or c[0].endswith(" on")]
    done = [c for c in chips if "done" in c[0]]
    aria = re.findall(r'aria-current="([^"]+)"', html)
    caption = re.findall(r'Step \d of 5[^<]*', html)
    print("%-28s chips=%d active=%d done=%d aria_step=%d caption=%r"
          % (label, len(chips), len(active), len(done), aria.count("step"),
             re.sub(r"\s+", " ", caption[0]).strip() if caption else None))
    if len(chips) != 5:
        sys.exit("expected 5 chips, got %r" % ([c[0] for c in chips],))
    if len(active) != 1:
        sys.exit("expected exactly one active chip, got %r" % ([c[0] for c in active],))
    if aria.count("step") != 1:
        sys.exit("expected exactly one aria-current=step, got %r" % (aria,))
    return html


login = get("/Login.aspx")
post("/Login.aspx", {"identifier": "bella", "password": "customer123",
                     "__csrf": csrf(login), "__VIEWSTATE": ""})

fresh = get("/App/CommissionRequest.aspx")
report("fresh GET", fresh)

base = {"__csrf": csrf(fresh)}

# Deliberately incomplete: no description, so the page bounces back with the
# form and this probe never creates a commission in the live database.
filled = dict(base)
filled.update({
    "cat": "1",
    "title": "150pc holographic vinyl sticker batch",
    "size": "3in",
    "budgetMin": "3500",
    "budgetMax": "4500",
    "deadline": "2026-12-01",
    "notes": "Deadline is flexible.",
})
html = post("/App/CommissionRequest.aspx", filled)
if "Request progress" not in html:
    sys.exit("POST did not re-render the form; probe may have submitted. head=%r"
             % re.sub(r"\s+", " ", html[:200]))
report("POST bounce (steps 1,2,4)", html)