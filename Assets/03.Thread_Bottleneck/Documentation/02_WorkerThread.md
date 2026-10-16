# 02 — 보조 무기 Job을 즉시 기다림

## 개발 상황과 증상

플레이어 주변을 도는 보조 무기 위치를 Job으로 계산했다. 예약 직후 Complete하여, 그 결과와 무관한 경험치·웨이브 갱신까지 뒤로 밀렸다. Job으로 옮겨도 화면은 계속 끊긴다.

## 문제 코드와 개선 코드

Problem: Schedule → Complete → IndependentGameWork → Consume.
Improved: Schedule → IndependentGameWork → Complete → Consume.

두 씬 모두 Job 지연 12ms, Main 독립 작업 지연 12ms와 동일한 위치 계산을 수행한다. ScheduleBatchedJobs도 동일하게 호출한다. 개선은 일을 줄이지 않고 실행 구간을 겹친다. Burst는 사용하지 않는다.

## 플레이와 관찰

CPU Timeline에서 Lab.Worker.Work, Lab.Main.IndependentWork, Lab.Worker.Complete와 실제 Job 샘플을 비교한다. Main이 Complete 중 직접 Job을 실행할 수도 있으므로 전부 유휴 대기로 보지 않는다.

성공 기준은 같은 입력 시각의 무기 좌표 일치, 두 작업 구간의 겹침, 프레임 시간 감소다. NativeArray는 Complete 후에만 읽고 종료 전에 완료·Dispose한다. Sleep은 실제 CPU 계산량의 대용이 아니다.

## 책의 추가 방식

이전 프레임 결과 소비 → 새 Job 예약 방식은 한 프레임 지연을 허용한다. 첫 프레임의 미완료/미예약 결과를 구분하고, 입력 시각을 캡처하며, 동일 simulation tick으로 결과를 비교해야 한다. 즉시 입력 반응이나 판정에 그대로 적용하지 않는다. 이 방식의 추가 씬은 초기 범위 밖이다.
