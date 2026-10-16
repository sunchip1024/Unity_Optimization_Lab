# 01 — 스폰 후보 준비를 매 프레임 반복

## 개발 상황과 증상

뱀서라이크에서 적을 맵 가장자리에 등장시키려고 32개 후보 좌표를 준비했다. 맵 반경은 고정인데 Update 경로에서 후보를 다시 만들면서 플레이 전체가 끊긴다.

## 문제 코드와 개선 코드

MainThreadProblem의 Tick은 매번 Build(game)을 호출한다. SpawnWork의 Build는 30ms 작업 지연 후 같은 좌표 배열을 채운다.

MainThreadImproved는 cachedRadius != mapRadius일 때만 Build한다. 처음 한 번 또는 입력이 바뀔 때 갱신하고 이후에는 동일한 배열을 재사용한다. Inspector의 mapRadius를 바꿔 무효화 경로를 확인할 수 있다.

## 플레이와 관찰

같은 이름의 Problem/Improved 씬을 따로 연다. 3초 이후 이동과 공전 무기의 연속성을 비교한다. 개선 씬의 첫 계산 프레임은 안정 구간과 분리한다.

CPU Timeline에서 Lab.Main.BuildSpawnPoints와 Main의 프레임 시간을 확인하고, 같은 시간대 Render의 명령 대기를 찾아본다. Wait 마커의 실제 표기는 환경에 따라 달라진다.

성공 기준은 후보 결과 일치, 개선 씬의 반복 생성 제거, 프레임 시간 감소다. Sleep은 계산 부하가 아닌 지연 모형이다. 실제 측정치는 workspace/Captures의 JSON과 RESULTS.md에 기록한다.
