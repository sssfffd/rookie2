# 61 — 맞추는 SQL 창 없애기 · 끌어다 놓기 · 코드 자리 (0.80)

## 주신 말

> 맞추는 sql창 없애고 io명 가로 크기를 더 키우셈
> 파일들 드래그해서 넣을 수 있게 해줘
> 그리고 2번 분석 안에 데이터 파싱하고 데이터들 집어 넣는 코드 위치가
> 어딘지 알려주셈

---

## 1) 맞추는 SQL 창을 없앴습니다

아래쪽 글 칸과 칸 나누미(GridSplitter)를 뺐습니다. 목록이 창 높이를
다 씁니다.

**기능은 그대로 둡니다.** 위의 `[SQL 글 저장]` 이 지금 차이로 글을
만들어 파일로 저장합니다 — 화면에 띄우지만 않습니다. 단추를 없애 버리면
만들어 둔 SQL 기능이 닿을 데가 없어지므로 남겼습니다. 단추까지 빼기를
원하시면 말씀해 주세요 (그러면 `DbSql.cs` 도 같이 뺄 수 있습니다).

IO명 칸을 **200 → 340** 으로 넓혔습니다.

```
IO명 340 · 이전 value 170 · 이후 value 170 · v1 110 · v2 110
```

---

## 2) 끌어다 놓기 — 왜 안 됐는지 찾았습니다

### 까닭: 칸 안의 TextBox 가 파일 드롭을 먹고 있었습니다

`TextBox` 는 기본으로 끌어다 놓기를 받습니다 (글자를 받으려고). 그런데
**파일**을 놓으면 받지도 않고 **위로 올려 보내지도 않습니다.** 칸의
넓이가 거의 전부 그 TextBox 라서, 칸 위에 파일을 놓으면 아무 일도 안
났습니다.

고친 것은 한 마디입니다.

```xml
<TextBox Text="{Binding BeforePath, Mode=TwoWay}" AllowDrop="False" ... />
```

이러면 WPF 가 그 TextBox 를 건너뛰고 바깥 칸(Border)에 놓습니다.
대신 글자를 끌어다 넣는 것은 안 됩니다 — 붙여넣기로 됩니다.

**눈으로는 절대 안 보이는 종류**라 검사기 규칙으로 박았습니다:
*놓는 칸 안의 TextBox 에 `AllowDrop="False"` 가 없으면 지적.* 되돌려
깨뜨려 잡는 것을 확인했습니다.

### 그리고 화면 아무 데나 놓을 수 있게 했습니다

전에는 작은 칸 두 개 위에만 놓을 수 있었습니다. 이제 **DB 화면 어디에
놓아도** 됩니다 (목록 위도 됩니다).

| 어떻게 놓으면 | 어디로 |
|---|---|
| **이전/이후 칸 위**에 하나 | 그 쪽 |
| **칸 밖**에 하나 | 비어 있는 쪽 (이전 → 이후 순서로 봅니다) |
| **둘을 한꺼번에** | 첫째가 이전, 둘째가 이후 |
| 칸 위에 둘 | 놓은 쪽부터 차례로 (이전 칸에 둘 → 이전 · 이후) |
| 셋 이상 | 앞의 둘만. **그렇다고 창을 띄워 알립니다** |

어느 쪽에 넣을지 **물어보지 않습니다** — 끌어다 놓는 중에 창이 뜨면
끌어 온 뜻이 없어집니다. 대신 들어간 자리가 칸에 글자로 보이므로
조용히 사라지지 않습니다.

셋 이상을 조용히 버리지 않는 이유: "넣었는데 왜 안 보이지" 가 가장
나쁜 결과입니다. 여러 파일을 **한쪽으로 묶어** 읽으시려면 그 파일들이
든 **폴더**를 놓으면 됩니다 — 폴더 안의 `.sql` / `.csv` / `.frm` 을
하위 폴더까지 다 읽습니다.

---

## 3) 분석 2 의 코드 자리

### 파일 → 자료 (파싱)

| 무엇을 읽나 | 자리 |
|---|---|
| **폴더 훑기** (하위 폴더까지, 어느 파일을 어느 읽기로 보낼지) | `src/LogScope.Db/DbRead.cs:179` `DbFolderReader.Read` |
| **`.sql` 덤프** (CREATE TABLE · INSERT) | `src/LogScope.Db/DbRead.cs:500` `SqlDumpReader.Read` |
| **`.csv` / `.tsv`** (파일 하나 = 표 하나) | `src/LogScope.Db/DbRead.cs:1237` `CsvTableReader.Read` |
| **`.frm`** (MySQL 5.x 표 정의, 모양만) | `src/LogScope.Db/DbReadMysql.cs:48` `FrmReader` |

**새 형식을 더하실 자리**는 `DbRead.cs` 의 `ReadTables` 입니다 —
확장자를 보고 갈라 보내는 곳이 거기 한 군데입니다.

### 자료 → 화면 목록 (집어넣기)

| 단계 | 자리 |
|---|---|
| 두 벌을 견주기 (표 → 열 → 행) | `src/LogScope.Db/DbCompare.cs` `DbDiff.Compare` |
| 차이를 **목록 줄로 펼치기** | `src/LogScope.Db/DbCompare.cs:628` `DbDiffList.Build` |
| **줄 하나에 값을 담는 자리** | `src/LogScope.Db/DbCompare.cs:795` `DbDiffList.OneRow` |
| 줄의 칸 정의 (`Io` · `Before` · `After` · `V1` · `V2`) | `src/LogScope.Db/DbCompare.cs` `DbDiffLine` |

**v1 · v2 를 채우실 자리가 `OneRow` 입니다.** 거기서 이렇게 하면 됩니다.

```csharp
int i = td.Before.IndexOf("v1");                 // 그 DB 의 열 이름
string v = i < 0 ? null : rd.Before.Get(i);
ln.V1 = DbRow.Show(v);
```

### 화면이 이 둘을 부르는 자리

```
src/LogScope.App/Db/DbView.xaml.cs:324   DbFolderReader.Read(path, null)   ← 읽기
src/LogScope.App/Db/DbView.xaml.cs:272   _vm.Take(snaps[0], snaps[1])      ← 넘기기
src/LogScope.App/Db/DbVm.cs:278          DbDiff.Compare(before, after)     ← 견주기
src/LogScope.App/Db/DbVm.cs:256          DbDiffList.Build(_diff, null)     ← 목록 줄
```

---

## 확인한 것 · 못 한 것

- `통과 654 / 실패 0`, 검사기 넷 지적 없음 (규칙 하나 늘어 아홉)
- 새 규칙은 되돌려 깨뜨려 잡는 것을 확인했습니다
- **Windows 에서 확인 못 했습니다**: SQL 창을 뺀 뒤의 목록 모양,
  IO명 340 폭, 그리고 **끌어다 놓기가 실제로 되는지**. `AllowDrop="False"`
  가 맞는 고침이라고 보지만 WPF 를 여기서 돌릴 수 없습니다 — 되는지
  꼭 알려 주세요. 아직 안 되면 `PreviewDrop` 으로 바꿔 보겠습니다.
