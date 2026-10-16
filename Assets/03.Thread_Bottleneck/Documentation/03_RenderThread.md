# 03 — 같은 적을 대량으로 개별 렌더링

## 개발 상황과 증상

뱀서라이크 후반 웨이브의 적 외형을 각각 MeshRenderer로 만들었다. 적이 수천 마리면 단순 로직이어도 렌더링 명령 제출 비용이 커질 수 있다.

## 문제 코드와 개선 코드

Problem은 같은 저복잡도 메시·머티리얼의 개별 CommandBuffer.DrawMesh 명령을 기록한다. Improved는 같은 난수 시드의 동일한 변환 행렬을 CommandBuffer.DrawMeshInstanced로 묶어 기록한다. 카메라의 같은 렌더 이벤트에서 실행하며 적 수를 줄이지 않는다. 초기 개별 MeshRenderer 방식은 Main의 컬링/관리 비용도 커서, 준비 단계에 명령을 기록해 제출 방식의 차이를 분리했다.

발표 PC 보정값은 추가 군집 40000개다. 설정은 두 씬의 CrowdWork에서 같은 값으로 변경한다. 군집은 AI/물리 없는 외형 실험이며 실제 플레이용 적 풀과 별개다. GPU 부하를 억제하기 위해 메시를 단순화하고 그림자는 사용하지 않는다. 객체 준비 비용은 측정에서 제외한다.

## 플레이와 관찰

CPU Timeline에서 Render의 실제 명령 처리와 Main을 함께 본다. Rendering의 Batches/SetPass도 확인한다. Frame Debugger는 별도 실행에서 비교한다.

성공 기준은 동일한 군집 외형, 드로우콜과 CPU 렌더 비용 감소다. 같은 물체를 많이 만들었다고 Render Thread 병목이 보장되지 않는다. Main의 컬링/Renderer 관리 또는 GPU가 먼저 지배하면 실제 병목을 표시한다. 인스턴싱 CPU 호출 역시 Main에서 수행된다는 점을 설명한다.

책의 극단적인 SetPass/Draw Call 수치를 맞추는 것이 목표가 아니다. 발표 기기에서 실제 스레드 데이터를 확인한다.
