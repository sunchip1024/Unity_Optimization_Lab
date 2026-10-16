# 검증 결과 — Nightfall Survivors
검증일: 2026-10-17 (Asia/Seoul)

## 환경과 플레이 체감 비교
Unity 6000.3.25f1 / Built-in / Direct3D 12 / Ryzen 7 7700 / RTX 3070 / RAM 32GB.
에디터 Game View 1280×720, 목표 60FPS, 동일 시드·자동 이동·검증용 무적 사용.
5초 워밍업 뒤 게임 시각 약 5~11초의 6초 구간에서 수집한 1차 검증이다. 에디터·도구 오버헤드를 포함하며 통계적 반복 벤치마크나 Player 전체 씬 성능 측정으로 해석하지 않는다.

| 실습 | 문제 중앙값 ms | 개선 중앙값 ms | P95 ms |
|---|---:|---:|---|
| 메인 스레드 | 34.66 | 16.77 | 35.92 → 18.22 |
| 워커 스레드 | 28.74 | 16.00 | 30.24 → 23.62 |
| 렌더 스레드 | 33.99 | 16.95 | 39.95 → 18.19 |
| GPU | 27.05 | 16.87 | 28.45 → 18.08 |

Main: 후보 재생성 제거. Worker: 두 작업량을 유지하고 실행 구간 중첩.
Render: 같은 40000개 행렬을 개별 DrawMesh와 묶음 DrawMeshInstanced로 제출.
GPU: 픽셀당 8192회 샘플 효과를 생략하는 품질 절충.
워커 개선 씬의 에디터 P95는 아직 60FPS 예산을 넘는다. 모든 프레임의 끊김이 사라졌다고 단정하지 않는다.

## 실제 CPU/GPU 프로파일
별도 무제한 FPS 실행에서 CPU Timeline을 기록했다. GPU 모듈은 GPU 실습에서만 켜서 CPU 렌더 실습의 계측 비용을 줄였다. 중첩 마커의 시간을 합산하지 않는다.
- Main 문제: Main의 Lab.Main.BuildSpawnPoints 약 30ms와 Render의 Gfx.WaitForGfxCommandsFromMainThread 대기. 개선에서는 반복 생성이 사라짐.
- Worker 문제: Main에서 Complete 도중 Job을 직접 수행하는 경우도 확인. 개선에서는 Worker 1/2 등의 Lab.Worker.Work와 Main 독립 작업이 겹치고 Complete는 매우 짧아짐.
- Render 문제: Render의 Camera.Render 약 30ms로 긴 렌더 명령 처리 확인. 개선은 묶음 제출로 큰 폭 감소. 기존 개별 Renderer 실험은 Main 비용도 커서 명령 버퍼 방식으로 분리했다.
- GPU 문제: GPU 시간 약 27ms와 GfxDeviceD3D12.WaitForGPU 대기. 개선 GPU 시간은 약 3ms대. 책의 Gfx.PresentFrame 대신 현재 API에서 보이는 마커를 기록했다.

원본 캡처는 workspace/2026-10-16_Chapter03_Thread_Bottleneck_Lab/Captures의 씬별 .data에서 Unity Profiler의 Load로 연다. 씬별 -profile.json에는 캡처 프레임 범위·실제 마커·시간을 저장했다. measurements-*.json은 위 플레이 비교의 원본이다. 두 측정 방식의 시간을 섞지 않는다.

## 게임 동작과 공유 자산
- 9개 씬 독립 로딩, 공통 GameRoot 프리팹/설정 참조, HUD 폰트·바 참조, 누락 스크립트 없음: 통과.
- 동일 입력·시각에서 후보 좌표 및 Job 위치 결과 일치: 통과.
- 캐시 재사용·입력 변경 시 무효화: 통과.
- 이동·경험치 레벨업·클리어·게임오버·재시작·풀 크기 유지: 통과.
- 책 요약 파일은 변경하지 않음.

## Windows Development Player
Builds/NightfallLab/NightfallLab.exe 빌드 성공. 최초 UnityLinker 실행의 접근 거부는 동일 명령을 제한 밖에서 재시도한 후 빌드 재시도로 해결했다.
Runtime Pipeline 연결 기능은 Player에서 비활성화돼 있지만 게임 실행과 에디터 연결에는 영향이 없다. 오프라인 게임에 Unity 서비스 프로젝트 연결은 필요하지 않다.

Development Player를 --lab-verify로 숨김 실행해 실제 180초 자동 플레이했다. 이 옵션은 정상 플레이에서 사용하지 않는다.
- 180.01초 생존 후 클리어, 처치 1256, 레벨 21, 약 10746프레임 진행.
- 마지막 900프레임 중앙값 16.67ms, P95 17.02ms.
- 클리어·게임오버·재시작·레벨업·동일 풀 크기: 모두 통과, 자동 종료.
- 검증 시 무적을 사용했다. 일반 플레이의 난이도/생존 밸런스 검증과 구분한다.
- 원본: Builds/NightfallLab/verification.json, verification-player.log.
- 문제·개선 8개 씬도 각각 8초의 Windows 기능 검사에서 종료·재시작·성장·풀 재사용을 통과했고 예외/인스턴싱 오류는 없었다.
- 숨김/배치 실행은 화면 렌더링이 생략될 수 있으므로, 이 검사 수치를 그래픽스 성능이나 화면 검증으로 사용하지 않는다. 전체 8개 실습의 가시 Player 전후 벤치마크는 별도 리허설로 남긴다.
- 인스턴싱 머티리얼의 변형 보존을 켜서 Player 빌드에서 필요한 셰이더 변형을 유지한다.

## 다음 리허설
발표 해상도·목표 기기를 맞추고 직접 조작해 각 씬 쌍을 비교한다. 필요하면 두 씬의 같은 부하 값을 함께 보정한다. URP 카메라 스택과 복잡한 수학 최적화는 확장으로 남긴다.
