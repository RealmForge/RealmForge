# 🌌 Realm Builder (RealmForge)
**PCG Planet Generator and Multiplayer Stack**

Unity의 차세대 기술 스택인 **DOTS**와 **NFE**를 활용하여, 복잡한 저수준 구조를 추상화하고 고성능 멀티플레이어 절차적 지형 생성을 지원하는 통합 API 솔루션입니다. 

<img width="385" height="232" alt="Image" src="https://github.com/user-attachments/assets/7402c0df-5438-4c4c-a0cd-4a83214d5d74" /> <img width="470" height="232" alt="Image" src="https://github.com/user-attachments/assets/d65769da-5705-4573-9f3d-4ccc5dd19413" />
---

## 🎯 1. 프로젝트 개요
* **배경**: Unity DOTS 및 NFE는 성능은 강력하지만, ECS 구조의 높은 학습 난이도와 복잡한 네트워킹 설정으로 인해 인디 개발자에게 진입 장벽이 큼.
* **목표**: 저수준 구조의 복잡성을 감춘 단순화된 API 기반 솔루션 프록시를 개발하여 개발 편의성 제공.
* **차별성**: 단순 성능 분석을 넘어 실제 게임 개발(PCG, Marching Cubes 등)에 즉시 활용 가능한 실용적 프레임워크 구현.

---

## 🛠️ 2. 주요 기술 스택
* **Engine**: Unity 6000.0.60f1 🎮
* **DOTS (ECS, Burst, Job System)**: 대규모 연산 및 데이터 처리 최적화  ⚡
* **Netcode for Entities (NFE)**: 데이터 지향 고성능 멀티플레이어 네트워킹  🌐
* **Octree**: 공간 분할을 통한 효율적인 LOD(Level Of Details) 관리  🌲
* **Marching Cubes**: 3D 스칼라 필드 데이터를 삼각형 메시로 변환하는 알고리즘  🧊
***Perlin Noise**: 절차적 지형 생성을 위한 연속적 난수 생성 함수  🎲

---

## 🏗️ 3. 시스템 아키텍처



### 3.1. 레이어 구조
* **데이터 처리 계층**: DOTS를 활용하여 대규모 연산과 구조적 데이터 흐름을 병렬화 및 최적화.
* **네트워크 계층**: NFE를 기반으로 엔티티 구조를 유지하며 멀티플레이 동기화 수행.
* **API 추상화 계층**: 내부 구조를 감추고 상위 개발자가 직관적으로 사용 가능한 프록시 제공.

### 3.2. 주요 모듈 구성
* **로비/세션 관리**: NFE 기반 P2P 매칭 및 엔티티 데이터 관리.
* **행성/절차적 생성**: 행성 단위를 청크로 관리하고 PRNG를 통해 데이터를 구체화.
* **데이터 시각화**: Marching Cubes 및 Job System을 사용하여 최적화된 병렬 메시 렌더링.
* **인게임 모듈**: 플레이어 조작, 구면 중력, 물리 상호작용 처리.

<img width="440" height="250" alt="Image" src="https://github.com/user-attachments/assets/495b8ab9-3c1f-4b81-afb0-6d85826cf01f" /> <img width="440" height="260" alt="Image" src="https://github.com/user-attachments/assets/148b2a10-cd32-4bee-ba99-5437a216d68a" />

---

## 🔄 4. 실행 및 동작 흐름

### 4.1. 사용자 시나리오 
1. **Package 설치**: Unity Package Manager를 통해 Git URL로 Repository 다운로드 📥
2. **Config 설정**: `Networking Config` 및 `PCG Config`를 통해 저수준/고수준 옵션 조정 ⚙️
3. **API 개발**: 제공되는 API를 통해 기술적 복잡성 없이 기존 방식대로 기능 구축 💻

### 4.2. 데이터 처리 흐름 

* 사용자 입력 ➡️ Perlin Noise 기반 점 집합 생성 ➡️ Octree 공간 분할 ➡️ Marching Cubes 적용 ➡️ DOTS 최적화 ➡️ NFE 동기화 ➡️ 행성 출력 🌍

---

## ✅ 5. 프로젝트 결과 및 성과
* **성능 개선**: DOTS 및 Burst 적용으로 기존 구조 대비 CPU 부하 및 맵 생성 시간 크게 단축
* **멀티플레이 안정성**: NFE 엔티티 단위 동기화와 Relay Service로 안정적인 4인 접속 환경 확인
* **LOD 구현**: 플레이어 위치 기반 실시간 지형 생성 및 소멸 시스템 구축으로 성능 효율 극대화

---

## 👥 6. 팀 정보 (Team Cash Hit)
* **구성원**: 전민규, 강신영, 김정현, 오규빈
* **소속**: 숭실대학교 (Soongsil University)
* **작성일**: 2025.12.31

---
