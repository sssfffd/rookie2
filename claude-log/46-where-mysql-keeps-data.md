# 46. MySQL 이 지금 데이터를 담고 있는 곳

> 근데frm 이랑 ibd읽는 거 bcl로 안되나?
> mysql이 현재 저장중인 data는 어디 담기는데?

## 한 곳이 아닙니다

이게 핵심입니다. `.ibd` 는 **가라앉은 부분**일 뿐입니다.

| 어디 | 무엇이 | 우리가 읽을 수 있나 |
|---|---|---|
| **메모리 (버퍼 풀)** | 바뀐 페이지. 아직 디스크에 안 쓴 것 | **못 읽습니다.** 파일이 아닙니다 |
| **redo 로그** `#innodb_redo\#ib_redoN` | 커밋은 됐지만 `.ibd` 에 아직 안 반영된 변경 | 읽으려면 재생기를 짜야 합니다 |
| **undo 테이블스페이스** `undo_001`, `undo_002` | 행의 **이전 판본** (되돌리기·일관된 읽기용) | 따로 파일입니다. `.ibd` 안에 없습니다 |
| **`<DB>\<표>.ibd`** | 가라앉은 값 + 표 정의 사본(SDI) | 네 (정의는 쉽고, 값은 조건부) |
| **`mysql.ibd`** | **진짜 데이터 사전**. 각 `.ibd` 의 SDI 는 사본입니다 | 네, 같은 방식으로 |
| **`ibdata1`** | 시스템 테이블스페이스 (변경 버퍼 등) | 우리 볼일 아닙니다 |
| **`binlog.00000N`** | 변경 이력 (켜 뒀으면) | 우리 볼일 아닙니다 |
| `*.frm` | 표 정의 — **5.x 에만** 있습니다 | 네, 역공학으로 |

8.0.30 부터 redo 로그가 `ib_logfile0`/`ib_logfile1` 에서 `#innodb_redo\` 폴더
안의 `#ib_redoN` 로 바뀌었습니다. 8.0 부터 undo 가 `ibdata1` 에서 떨어져 나와
제 파일이 됐습니다.

## 그래서 돌고 있는 서버의 `.ibd` 를 복사하면

두 가지가 동시에 생깁니다.

1. **방금 고친 값이 `.ibd` 에 아직 없을 수 있습니다.** 메모리나 redo 에만
   있습니다.
2. **`.ibd` 에 있는 값이 지금 값이 아닐 수 있습니다.** 누가 고치는 중이면
   원래 값은 undo 에 있고, 되돌려질지 아닐지는 그 파일만 봐서는 모릅니다.

둘 다 **조용히** 일어납니다. 파일이 깨지지도 않고, 읽기가 실패하지도 않습니다.
그냥 **틀린 값이 멀쩡한 얼굴로** 나옵니다. 견주는 도구에서 이게 제일 나쁩니다 —
없는 차이를 만들거나 있는 차이를 가리고, 그걸로 `UPDATE` 글까지 만들어 줍니다.

## 그러면 값은 어떻게 받나 — 세 길

| 길 | 어떻게 | 믿을 수 있나 |
|---|---|---|
| **`mysqldump`** | 서버가 일관된 읽기를 해서 뽑아 줍니다 | **네.** 지금 쓰는 길입니다 |
| **서버를 끄고 복사** | 정상 종료 때 메모리·redo 가 다 `.ibd` 로 들어갑니다 | **네** |
| **`FLUSH TABLES ... FOR EXPORT`** | 그 표만 밀어내고 `.cfg` 를 함께 만듭니다. 잠금 유지 중 복사 | **네** |
| 돌고 있는 폴더를 그냥 복사 | — | **아니오** |

**모양(이름·타입)은 다릅니다.** 표 정의는 `ALTER` 를 돌리지 않는 동안 바뀌지
않으니, 돌고 있는 폴더의 `.ibd` 에서 SDI 만 꺼내 읽는 것은 괜찮습니다.

## 내 PC 에서 그 자리 찾기

```sql
SHOW VARIABLES LIKE 'datadir';
SHOW VARIABLES LIKE 'innodb_undo_directory';
SHOW VARIABLES LIKE 'innodb_log_group_home_dir';
```

윈도우 설치본 기본값:

```
C:\ProgramData\MySQL\MySQL Server 8.0\Data\     ← datadir
C:\ProgramData\MySQL\MySQL Server 8.0\my.ini    ← 설정
```

`ProgramData` 는 탐색기에서 **숨겨져 있습니다.** 주소 칸에 직접 쳐 넣으세요.

## 정리 — BCL 로 무엇이 되나

- **표·열 이름과 타입**: 됩니다. `.ibd` 안의 SDI(zlib 로 눌린 JSON)를 풀면
  되고, `DeflateStream` 으로 풀립니다. `ibd2sdi` 호출이 없어집니다.
- **값**: BCL 이 막는 게 아니라, **그 파일에 정답이 없을 수 있다**는 게
  막습니다. 서버를 끈 복사본이면 되지만, **끈 복사본인지를 파일만 보고
  확인할 방법이 없습니다.**
- **`.frm`**: 됩니다. 5.x 전용이고 얻는 것은 모양뿐입니다.

출처: [MySQL 8.0 Reference Manual — Redo Log](https://dev.mysql.com/doc/refman/8.0/en/innodb-redo-log.html) ·
[Undo Tablespaces](https://dev.mysql.com/doc/refman/8.4/en/innodb-undo-tablespaces.html) ·
[Serialized Dictionary Information](https://dev.mysql.com/doc/refman/8.0/en/serialized-dictionary-information.html) ·
[InnoDB physical files on MySQL 8.0 (mydbops)](https://www.mydbops.com/blog/innodb-physical-files-on-mysql-80) ·
[innodump 의 한계 목록](https://github.com/PrzemekMalkowski/innodump)

---

## 뒷이야기 — "MySQL 을 쓰는 프로그램" 의 데이터

> 아니 내 프로그램 말고 mysql을 활용해서 동작하는 프로그램은 데이터가
> 어디에 저장되어있는지

위의 표가 "어느 파일에 무엇이" 였다면, 이건 "**어느 폴더를 가리켜야 하나**"
입니다. 답은 간단합니다 — **`datadir` 안의 DB 이름 폴더 하나**, 그 안에
**표마다 파일 하나**(`mydb\recipe.ibd`). 파티션을 쓰면 조각마다 한 파일
(`log#p#p202603.ibd`).

그래서 `db.before` / `db.after` 에 적을 것은 그 폴더입니다. `Data` 를 그대로
가리켜도 됩니다 — 0.54 부터 하위 폴더까지 읽고 표 이름 앞에 폴더 이름을
붙이니 DB 가 여럿이어도 섞이지 않습니다.

**단정하기 전에 봐야 할 셋**

1. **`innodb_file_per_table` 가 꺼져 있으면 `.ibd` 가 아예 없습니다.** 표가 전부
   `ibdata1` 한 덩이 안에 들어갑니다. 5.6 부터 기본이 켬이지만 오래된 설비는
   꺼 둔 것이 있습니다. 이 경우 파일로 견주는 길은 막히고 `mysqldump` 뿐입니다.
2. **MyISAM 이면 `표.MYD` + `표.MYI`** 입니다. 우리는 아직 이 둘을 읽지
   않습니다. 설비 쪽 오래된 프로그램에 흔합니다.
3. 표를 만들 때 `DATA DIRECTORY` 를 줬으면 그 표만 다른 디스크에 있습니다.

**그래서 추측하지 말고 물어보는 쿼리를 README 에 넣었습니다.**
`information_schema.processlist` 로 그 프로그램이 어느 DB 를 쓰는지,
`innodb_tablespaces` + `innodb_datafiles` 로 표마다 실제 파일 경로를 봅니다
(5.7 은 `innodb_sys_*`).

**가장 쉽게 놓칠 자리**: 설비 프로그램은 접속 정보와 장비 설정을 자기 폴더의
`.ini`/`.xml` 이나 레지스트리에 두고 DB 에는 레시피·알람·이력만 두는 경우가
많습니다. **DB 만 견주면 그 파일들의 차이는 통째로 안 보입니다.** 거기까지
견주려면 따로 말씀해 주셔야 합니다 — 로그 쪽은 이미 파일을 읽으니 어렵지
않습니다.

출처 추가: [INFORMATION_SCHEMA.INNODB_DATAFILES](https://dev.mysql.com/doc/refman/8.0/en/information-schema-innodb-datafiles-table.html) ·
[File-Per-Table Tablespaces](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/innodb-file-per-table-tablespaces.html)
