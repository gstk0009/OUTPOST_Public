<div align="center">

# OUTPOST Z: DEFENSE PROTOCOL

### 감염자들의 공세를 버티고, 마지막 구조 지점까지 살아남아라.

<br>

<img src="https://img.shields.io/badge/STATUS-IN%20DEVELOPMENT-222222?style=for-the-badge"/>
<img src="https://img.shields.io/badge/UNITY-6000.3.12f1-111111?style=for-the-badge&logo=unity&logoColor=white"/>
<img src="https://img.shields.io/badge/C%23-GAME%20LOGIC-512BD4?style=for-the-badge&logo=csharp&logoColor=white"/>
<img src="https://img.shields.io/badge/GENRE-FPS%20%7C%20ZOMBIE%20DEFENSE-6B1E1E?style=for-the-badge"/>

<br><br>

> **현재 개발 중인 프로젝트입니다.**
> 첫 번째 스테이지의 기본 플레이 흐름을 완성했고,
> 현재는 새로운 스테이지와 스토리를 하나씩 확장해 나가고 있습니다.

<br>

[게임 소개](#-게임-소개) ·
[플레이](#-플레이) ·
[주요 시스템](#-주요-시스템) ·
[개발 현황](#-개발-현황) ·
[앞으로](#-앞으로) ·
[개발 기록](#-개발-기록) ·
[조작법](#-조작법)

</div>

---

## 🎮 게임 소개

**OUTPOST Z**는 감염 사태가 발생한 군사 전초기지를 배경으로 한
**싱글 플레이 FPS 좀비 디펜스 게임**입니다.

몰려오는 감염자들을 상대하며 탄약과 체력을 관리하고,
중반부터는 발전기를 지켜야 합니다.

마지막 공세까지 버텨낸 뒤에는 헬기 착륙 지점으로 이동해
구조 헬기를 타고 지역을 빠져나가는 것이 현재 스테이지의 목표입니다.

<br>

> **방어 → 목표 수행 → 최종 공세 → 탈출**

단순히 같은 장소에서 웨이브 숫자만 올라가는 것보다는
플레이가 진행될수록 **목표와 상황이 자연스럽게 바뀌는 흐름**을 만들고 있습니다.

---

## 📼 플레이

<div align="center">

<img src="./docs/README_Gameplay.gif" width="850" alt="OUTPOST Z Gameplay"/>

</div>

<br>

<table>
<tr align="center">
<td width="180">

### 01

**전초기지 진입**

전투 준비

</td>
<td width="40">

### →

</td>
<td width="180">

### 02

**웨이브 방어**

감염자 공세

</td>
<td width="40">

### →

</td>
<td width="180">

### 03

**발전기 방어**

목표물 보호

</td>
</tr>

<tr align="center">
<td width="180">

### 04

**최종 공세**

마지막 생존전

</td>
<td>

### →

</td>
<td width="180">

### 05

**헬기 탈출**

스테이지 종료

</td>
<td></td>
<td></td>
</tr>
</table>

---

## ⚙ 주요 시스템

<table>
<tr>
<td width="50%">

### WAVE DEFENSE

단계별로 몰려오는 감염자들을 상대합니다.

웨이브가 진행될수록 적의 구성과 전투 상황이 달라집니다.

</td>
<td width="50%">

### GENERATOR DEFENSE

중반부터는 플레이어의 생존뿐 아니라
**발전기까지 함께 지켜야 합니다.**

</td>
</tr>

<tr>
<td>

### ZOMBIE AI

`NavMesh` 기반으로 이동하며
상황에 따라 **플레이어 / 발전기**를 추적합니다.

</td>
<td>

### WEAPON

Rifle · Sniper · Pistol

발사, 재장전, 반동, 탄퍼짐, 스코프 등의
FPS 기본 전투 시스템을 구현했습니다.

</td>
</tr>

<tr>
<td>

### HUD & MINIMAP

체력, 탄약, 무기 슬롯, 현재 웨이브와 목표 등
전투에 필요한 정보를 HUD로 제공합니다.

</td>
<td>

### EXTRACTION

마지막 웨이브가 끝나면 전투가 바로 끝나는 것이 아니라
헬기 착륙 지점까지 이동해 **실제로 탈출해야 합니다.**

</td>
</tr>
</table>

---

## 🚧 개발 현황

### `NOW`

현재는 **첫 번째 스테이지의 기본 플레이 사이클**을 완성한 상태입니다.

```text
전초기지 진입
      ↓
감염자 웨이브
      ↓
발전기 활성화
      ↓
발전기 방어
      ↓
최종 공세
      ↓
헬기 탈출
```

FPS 전투부터 좀비 AI, 웨이브, 목표물 방어, HUD,
마지막 헬기 탈출까지 한 판의 시작과 끝이 이어지도록 만들었습니다.

현재 스테이지는 완성된 게임이라기보다
**앞으로 만들 게임의 첫 번째 챕터에 가깝습니다.**

<br>

### `NEXT`

다음 작업부터는 기존 시스템만 계속 다듬기보다는
**실제 게임의 볼륨을 늘리는 작업**에 집중할 예정입니다.

* 새로운 스테이지
* 새로운 감염자
* 스테이지별 목표
* 무기와 아이템 추가
* 이벤트 / 연출
* 스토리 진행

---

## 🗺 앞으로

지금의 전초기지를 탈출하면 끝나는 게임으로 만들 생각은 없습니다.

```text
OUTPOST
   │
   └── 탈출
        │
        ▼
   NEXT AREA
        │
        ├── 새로운 환경
        ├── 새로운 감염자
        ├── 새로운 임무
        └── 새로운 이야기
             │
             ▼
         NEXT STAGE ...
```

다음 스테이지에서는 단순히 배경만 바꾸는 것이 아니라
**플레이어가 해야 하는 일 자체가 달라졌으면 합니다.**

예를 들면,

`폐쇄된 시설 탐색`
`물자 확보`
`생존자 구조`
`특정 지역 방어`
`이동 중 습격`
`새로운 탈출 방법`

같은 식입니다.

그리고 스테이지가 이어지면서

**왜 감염이 시작됐는지**,
**주인공이 어디로 향하고 있는지**,
**각 지역에서 무슨 일이 있었는지**

조금씩 알 수 있도록 스토리도 같이 만들어갈 생각입니다.

<br>

> ### 목표
>
> **웨이브를 많이 넣는 게임보다,
> 다음 스테이지가 궁금한 좀비 게임을 만들고 싶습니다.**

---

## 🛠 개발 기록

<div align="center">

|                   |                                    |
| :---------------- | :--------------------------------- |
| **Engine**        | Unity `6000.3.12f1`                |
| **Language**      | C#                                 |
| **Async**         | UniTask                            |
| **AI Navigation** | Unity NavMesh                      |
| **Genre**         | Single Player FPS / Zombie Defense |
| **Status**        | In Development                     |

</div>

<br>

<details>
<summary><b>구현한 내용 조금 더 보기</b></summary>

<br>

### Game Flow

* 웨이브 준비 / 시작 / 종료 관리
* 발전기 활성화 및 방어 목표
* 최종 웨이브
* 헬리콥터 탈출
* Victory / Failed 흐름

### Combat

* 공통 Weapon 구조
* Rifle / Sniper / Pistol
* 재장전
* 반동
* 탄퍼짐
* 스코프
* 데미지 처리

### Enemy

* NavMesh 기반 이동
* 플레이어 추적
* 발전기 추적
* 공격
* 적 스폰 및 웨이브 관리

### ETC

* ScriptableObject 기반 웨이브 데이터
* UniTask 비동기 처리
* HUD / Minimap
* 전투 BGM
* 일부 FX / Object Pooling

</details>

---

## 🕹 조작법

<div align="center">

|       KEY       | ACTION      |
| :-------------: | :---------- |
| `W` `A` `S` `D` | 이동          |
|     `Mouse`     | 시점 이동       |
|   `Left Shift`  | 달리기         |
|   `Left Ctrl`   | 앉기          |
|     `Space`     | 점프          |
|   `Left Click`  | 공격          |
|  `Right Click`  | 보조 동작 / 스코프 |
|       `R`       | 재장전         |
|   `1` `2` `3`   | 무기 변경       |
|  `Mouse Wheel`  | 무기 순환       |
|      `Esc`      | 커서 잠금 전환    |

</div>

---

<div align="center">

### OUTPOST Z: DEFENSE PROTOCOL

**IN DEVELOPMENT**

`Unity 6000.3.12f1`

<br>

처음 만든 전초기지에서 시작해서,
조금씩 더 큰 게임으로 만들어가고 있습니다.

</div>
