"""Ad-hoc HTTP probe for the commission pages. Not part of the site build."""
import http.cookiejar
import re
import sys
import urllib.parse
import urllib.request

BASE = "http://localhost:8095"
OPENER = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
    urllib.request.HTTPRedirectHandler(),
)


def get(path):
    req = urllib.request.Request(BASE + path, headers={"User-Agent": "probe"})
    return OPENER.open(req, timeout=60).read().decode("utf-8", "replace")


def post(path, fields):
    data = urllib.parse.urlencode(fields).encode()
    req = urllib.request.Request(BASE + path, data=data, headers={"User-Agent": "probe"})
    return OPENER.open(req, timeout=60).read().decode("utf-8", "replace")


def csrf(html):
    m = re.search(r'name="__csrf"[^>]*value="([^"]+)"', html)
    if not m:
        m = re.search(r'value="([^"]+)"[^>]*name="__csrf"', html)
    return m.group(1) if m else ""


login = get("/Login.aspx")
post("/Login.aspx", {
    "identifier": "bella",
    "password": "customer123",
    "__csrf": csrf(login),
    "__VIEWSTATE": "",
})

target = sys.argv[1] if len(sys.argv) > 1 else "/App/CommissionDetail.aspx?id=1"
html = get(target)
print("URL", target, "len", len(html))
err = re.search(r'class="alert[^"]*"[^>]*>([^<]{0,200})', html)
print("ALERT:", err.group(1).strip() if err else "(none)")

chips = re.findall(r'class="step([^"]*)"', html)
print("CHIPS:", chips)
print("ARIA-CURRENT:", re.findall(r'aria-current="([^"]+)"', html))
print("RAIL-LINE:", re.findall(r'Step \d of 5[^<]*', html))
print("MARKER-REFSOK:", "__refsOk" in html)
print("H2:", re.findall(r'<h1[^>]*>([^<]{0,80})', html)[:3])