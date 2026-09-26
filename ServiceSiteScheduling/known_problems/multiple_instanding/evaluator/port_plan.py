import json, sys

src, dst = sys.argv[1], sys.argv[2]
d = json.load(open(src))

def conv_resource(r):
    if "trackPartId" in r:
        return {"kind": "trackPart", "id": int(r["trackPartId"])}
    if "facilityId" in r:
        return {"kind": "facility", "id": int(r["facilityId"])}
    raise ValueError(f"unknown resource shape: {r}")

actions = []
for a in d["actions"]:
    tt = a["taskType"]
    su = a["shuntingUnit"]
    actions.append({
        "startTime": int(a["startTime"]),
        "endTime": int(a["endTime"]),
        "taskType": {
            "predefined": tt.get("predefined"),
            "other": tt.get("other"),
        },
        "shuntingUnit": {
            "id": int(su["id"]),
            "memberIDs": [int(m["id"]) for m in su["members"]],
            "parentIDs": [],
            "childIDs": [],
        },
        "location": int(a["location"]),
        "resources": [conv_resource(r) for r in a.get("resources", [])],
    })

out = {"schemaVersion": 2, "actions": actions}
json.dump(out, open(dst, "w"), indent=2)
print(f"wrote {len(actions)} actions to {dst}")
