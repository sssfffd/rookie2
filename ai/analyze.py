#!/usr/bin/env python3
"""LogScope 선택 기능 — 비교 결과 요약기 (자리 표시용 예제).

이 파일은 "옵션 기능" 입니다. 프로그램은 이게 없어도 그대로 돕니다.
설정 창의 [AI 모듈 (선택)] 에서 켜고 python.exe 와 이 파일의 경로를
지정해야만 불립니다.

동작 방식
---------
표준 입력으로 JSON 하나를 받고, 표준 출력으로 JSON 하나를 돌려줍니다.

    입력  {"schema": "logscope.compare.v1",
           "beforeChannels": 200, "afterChannels": 201,
           "commonChannels": 199, "changedChannels": 12,
           "onlyBefore": ["..."], "onlyAfter": ["..."],
           "changed": [{"name": "...", "maxAbs": 1.0, "meanAbs": ...,
                        "rms": ..., "area": ..., "timeRatio": ...,
                        "diffSamples": 20, "segments": 1}, ...]}

    출력  {"summary": "사람이 읽을 소견 글"}

지금은 규칙 몇 개로 글을 만들 뿐이고, 모델을 부르지 않습니다.
나중에 여기를 직접 구현하면 됩니다.

지켜야 할 것
------------
* 표준 라이브러리만 씁니다 (json, sys). 설치할 것이 없습니다.
* 네트워크를 쓰지 않습니다. 바깥으로 나가는 코드를 넣기 전에,
  로그 내용이 밖으로 나가도 되는지 먼저 확인하세요.
* 받는 것은 채널 이름과 숫자 요약뿐입니다. 로그 파일이나 파일 경로는
  넘어오지 않습니다.
"""

import json
import sys


def classify(item):
    """차이의 모양을 한 마디로."""
    segments = item.get("segments", 0)
    ratio = item.get("timeRatio", 0.0)
    if segments >= 10 and ratio > 0.3:
        return "계속 흔들림"
    if segments <= 2 and ratio > 0.3:
        return "한 구간이 통째로 어긋남"
    if segments <= 2:
        return "짧게 한두 번 튐"
    return "군데군데 다름"


def build_summary(data):
    lines = []

    before = data.get("beforeChannels", 0)
    after = data.get("afterChannels", 0)
    changed = data.get("changedChannels", 0)
    only_before = data.get("onlyBefore", [])
    only_after = data.get("onlyAfter", [])

    lines.append("이전 로그 IO %d개, 이후 로그 IO %d개." % (before, after))

    if only_before:
        lines.append("이전에만 있는 IO %d개: %s"
                     % (len(only_before), ", ".join(only_before[:8])
                        + (" 외" if len(only_before) > 8 else "")))
    if only_after:
        lines.append("이후에만 있는 IO %d개: %s"
                     % (len(only_after), ", ".join(only_after[:8])
                        + (" 외" if len(only_after) > 8 else "")))
    if not only_before and not only_after:
        lines.append("IO 이름은 양쪽이 같습니다.")

    if changed == 0:
        lines.append("허용 오차 안에서 값이 달라진 IO 는 없습니다.")
        return "\n".join(lines)

    lines.append("값이 달라진 IO %d개." % changed)

    items = data.get("changed", [])
    shaky = [i for i in items if i.get("segments", 0) >= 10]
    if shaky:
        lines.append(
            "이 중 %d개는 짧게 반복해서 흔들립니다(%s). "
            "차이 총합이나 면적만 보면 크게 나오지만, 최대 차이는 작을 수 있습니다."
            % (len(shaky), ", ".join(i["name"] for i in shaky[:5])))

    by_max = sorted(items, key=lambda i: i.get("maxAbs", 0.0), reverse=True)
    lines.append("가장 크게 벌어진 순서:")
    for item in by_max[:5]:
        lines.append("  - %s : 최대 %.4g, 평균 %.4g, 구간 %d개 (%s)"
                     % (item.get("name", "?"),
                        item.get("maxAbs", 0.0),
                        item.get("meanAbs", 0.0),
                        item.get("segments", 0),
                        classify(item)))

    return "\n".join(lines)


def main():
    try:
        data = json.load(sys.stdin)
    except ValueError as err:
        json.dump({"summary": "입력을 읽지 못했습니다: %s" % err},
                  sys.stdout, ensure_ascii=False)
        return 1

    if data.get("schema") != "logscope.compare.v1":
        json.dump({"summary": "모르는 입력 형식입니다: %r" % data.get("schema")},
                  sys.stdout, ensure_ascii=False)
        return 1

    json.dump({"summary": build_summary(data)}, sys.stdout, ensure_ascii=False)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
