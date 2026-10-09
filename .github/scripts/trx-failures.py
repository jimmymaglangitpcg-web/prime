"""Reports failed tests from .trx files as GitHub annotations and in the job summary (docs/TESTING.md §4).

usage: python3 trx-failures.py <results directory>
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
failures = []
for path in glob.glob(os.path.join(sys.argv[1], "**", "*.trx"), recursive=True):
    for result in ET.parse(path).getroot().iterfind(".//t:UnitTestResult", NS):
        if result.get("outcome") != "Failed":
            continue
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", default="", namespaces=NS).strip()
        trace = result.findtext("t:Output/t:ErrorInfo/t:StackTrace", default="", namespaces=NS).strip()
        failures.append((result.get("testName", "?"), message, trace.splitlines()[0] if trace else ""))

print(f"{len(failures)} failed test(s)")
# GitHub shows at most ten error annotations per step; the summary lists them all.
for name, message, where in failures[:10]:
    text = f"{message} | {where}".replace("%", "%25").replace("\r", "").replace("\n", " | ")[:900]
    print(f"::error title={name[:200]}::{text}")
summary = os.environ.get("GITHUB_STEP_SUMMARY")
if summary:
    with open(summary, "a", encoding="utf-8") as out:
        out.write(f"### {len(failures)} failed test(s)\n\n")
        for name, message, where in failures:
            out.write(f"- **{name}**: {message.splitlines()[0] if message else ''} ({where})\n")
