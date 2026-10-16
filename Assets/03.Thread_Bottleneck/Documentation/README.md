# Nightfall Survivors — 3장 스레드와 병목 실습

## 먼저 플레이하기

1. Unity 6000.3.25f1에서 Scenes/00_Template.unity를 열고 Play한다.
2. Game 창을 클릭하고 WASD/방향키로 이동한다. 가까운 적을 자동 공격한다.
3. 금색 보석을 수집하면 자동으로 레벨업한다. SPACE는 주변 적을 공격하는 Soul Nova다.
4. 180초 생존하면 클리어, 체력이 0이면 게임오버다. R로 다시 시작한다.

| 키 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| SPACE | Soul Nova, 8초 재사용 대기 |
| R | 같은 씬 재시작. 기존 풀을 재사용 |
| F2 | 고정 경로 자동 플레이 켜기/끄기 |
| F3 | FPS·프레임 시간·실습 작업 시간 진단 표시 |
| F4 | 목표 60FPS / 프레임 제한 해제 전환 |
| F5 / F6 | Build Settings의 이전 / 다음 씬 |

기본 조작에서는 무적이 아니다. 자동 플레이도 일반 게임 규칙을 따른다. 검증 도구가 켜는 testInvulnerable은 테스트 전용이며 씬의 기본값은 꺼져 있다.

## Template와 복사본

00_Template를 실제로 복사해 네 종류의 Problem/Improved 씬 8개를 만들었다. 모든 씬은 Prefabs/GameRoot.prefab과 Settings/GameSettings.asset을 공유한다. 공통 게임·HUD·전장·캐릭터는 프리팹에서 수정하고, 각 실습의 차이는 Experiment 컴포넌트(또는 GPU 씬의 Camera 컴포넌트)에 둔다.

게임 전체 코드를 8번 복제하지 않았다. 실습을 다시 생성하는 도구는 AgentScripts/LabSceneBuilder.cs에 있으며 기존 씬을 자동 덮어쓰지 않는다. 공통 변경은 프리팹을 수정하면 반영된다.

Windows 실행 파일: 프로젝트 루트 Builds/NightfallLab/NightfallLab.exe. F5/F6으로 씬을 순서대로 바꿀 수 있다. 에디터와 별도로 180초 자동 실행 검사도 통과했다. 개발 빌드의 --lab-verify는 무적 자동 검증 후 verification.json을 쓰고 종료하는 전용 옵션이며 일반 플레이에는 사용하지 않는다.

## 실습 순서

| 실습 | 문제 씬 → 개선 씬 | 읽을 코드 |
|---|---|---|
| 스폰 준비 | 01_MainThread_Problem → Improved | MainThreadProblem / MainThreadImproved / SpawnWork |
| Job 동기화 | 02_WorkerThread_Problem → Improved | WorkerThreadProblem / WorkerThreadImproved / WeaponJobWork |
| 적 렌더링 | 03_RenderThread_Problem → Improved | RenderThreadProblem / RenderThreadImproved / CrowdWork |
| 필살기 효과 | 04_GPU_Problem → Improved | GpuProblem / GpuImproved / LabUltimate.shader |

각 실습은 3초부터 부하가 적용된다. 우선 직접 이동하고 투사체·공전 무기·카메라 움직임의 끊김을 비교한다. 이후 같은 조건에서 Timeline을 확인한다. 게임의 자세한 상황과 문제·수정 코드는 개별 안내를 참고한다.

- [메인 스레드](01_MainThread.md)
- [워커 스레드](02_WorkerThread.md)
- [렌더 스레드](03_RenderThread.md)
- [GPU](04_GPU.md)

## 비교 조건

플레이 체감은 기본 60FPS 목표로 비교한다. 원인 분석은 F4로 제한을 해제한 별도 실행에서 CPU Timeline/Rendering/GPU를 확인한다. 해상도·씬 쌍 설정·난수 시드·게임 진행 시점을 맞춘다. 자동 플레이는 같은 경로를 쓰지만 서로 다른 프레임 간격 때문에 전투 결과가 비트 단위로 같다고 가정하지 않는다.

Main/Worker의 Sleep은 CPU 계산량이 아니라 작업 지연과 동기화를 설명하기 위한 모형이다. 실제 게임에 Sleep을 넣는 최적화 기법으로 해석하지 않는다. 렌더/GPU는 실제 그래픽스 작업을 수행한다.

GPU 효과는 실습을 위해 계속 표시한다. 저사양 씬은 효과를 생략하므로 품질과 성능의 절충이다. 렌더 군집은 AI/물리가 없는 추가 적 외형으로 제출 비용만 분리한다. 많은 객체만으로 Render Thread 병목을 단정하지 말고 실제 스레드 작업을 확인한다.

## 재현 도구

프로젝트 루트의 AgentScripts/LabVerification.cs:
- ConfigureGameView: 에디터 Game 창을 1280×720 고정 크기로 설정.
- ValidateAssets: Play 정지 상태에서 9개 씬의 공통 프리팹·참조·스크립트 검사.
- CompareRange(start,count,captureSeconds): Play 중 지정 씬을 자동 비교. 5초 워밍업 후 같은 시간 구간을 기록.
- VerifyGameRules: Play 중 이동·레벨업·클리어·게임오버·재시작과 풀 크기 검사.
- VerifyAlgorithms: Play 중 같은 입력의 후보/Job 결과와 캐시 재사용·무효화 검사.

AgentScripts/LabProfiling.cs의 Capture는 별도 무제한 FPS 실행에서 씬별 .data와 마커 요약을 기록한다. 이전에 도구가 기록한 메모리 내 프레임은 디스크 저장 후 다음 캡처 전에 비운다. GPU 모듈은 GPU 실습에서만 켠다. [실측 결과](RESULTS.md)를 참고한다.

측정 JSON/캡처는 Git 제외된 workspace 작업 폴더에 저장한다. 해당 폴더가 없어도 게임은 실행된다. 에디터 측정에는 도구·에디터 부하가 포함된다. 발표 기기의 실제 결과로 부하를 보정하고 Development Player 측정과 구분한다.

## 범위

구현: 기본 전투·성장·180초 종료, 풀링, 로우폴리 전장·캐릭터, TMP/uGUI HUD, 9개 씬.
확장: URP 카메라 스택, 책의 다음 프레임 결과 활용, 복잡한 수학/후처리 최적화.
