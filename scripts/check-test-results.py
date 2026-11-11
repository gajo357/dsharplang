"""Fail closed on absent, zero, skipped, failed or inconsistent VSTest evidence."""
import json
import sys
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results = root.findall(".//t:UnitTestResult", ns)
counters = root.find(".//t:Counters", ns)
assert counters is not None and results, "No tests discovered"
assert len({r.attrib["testId"] for r in results}) == len(results), "Duplicate test results"
assert all(r.attrib.get("outcome") == "Passed" for r in results), "Failed or skipped tests"
for field in ("total", "executed", "passed"):
    assert int(counters.attrib[field]) == len(results), f"Inconsistent {field} count"
assert root.find(".//t:ResultSummary", ns).attrib["outcome"] == "Completed", "Incomplete test run"
print(json.dumps({"suite": "compiler", "total": len(results), "passed": len(results), "skipped": 0}))
