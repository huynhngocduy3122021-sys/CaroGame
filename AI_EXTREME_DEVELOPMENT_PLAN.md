# Kế hoạch phát triển AI Caro – Hard trước, Extreme sau

## 1. Mục tiêu và phạm vi

Mục tiêu trước mắt là hoàn thiện chế độ **Hard** chạy ổn định trên PC và Mobile bằng:

```text
Luật dùng chung
    ↓
Tactical Scanner
    ↓
Candidate Generator
    ↓
Alpha-Beta Search
    ↓
Safety Validation
    ↓
Nước đi cuối cùng
```

Sau khi Hard vượt qua benchmark mới mở rộng sang **Extreme** bằng ML-Agent, self-play và league snapshot.

Không train lại model ngay ở giai đoạn đầu. Model ML hiện tại chỉ được giữ làm baseline hoặc dùng để xếp thứ tự nước đi sau khi runtime search đã ổn định.

## 2. Trạng thái code hiện tại

- Bàn cờ gameplay hiện đang hard-code `31 x 19`; đây chỉ là baseline cũ, không phải giới hạn thiết kế. Kích thước mục tiêu phải được cấu hình để hỗ trợ 50×50 hoặc 100×100.
- Đã có `CaroAiBoard`, `CaroAiRules`, `CaroAiMoveGenerator`, `CaroAiEvaluator`, `CaroAlphaBetaSearch` và `CaroSearchGameplayAgent`.
- `NetworkManagerUI` hiện mặc định `useSearchAI = true`, vì vậy Single Play đang chạy Alpha-Beta thuần, chưa phải ML-Agent + Search hybrid.
- Model `Assets/Models/CaroAgent.onnx` chỉ được dùng khi tắt `useSearchAI`.
- `CaroAgent` training hiện vẫn đấu với đối thủ heuristic/ngẫu nhiên; chưa có self-play hai Agent và snapshot league.
- Chưa có Tactical Scanner độc lập, Transposition Table, Evaluation Runner và bộ test AI hoàn chỉnh.

## 3. Quy tắc kỹ thuật phải chốt trước khi phát triển

- Dùng một nguồn cấu hình và luật duy nhất cho training, search, gameplay, board generator, visual manager và camera.
- Không hard-code kích thước cố định trong AI. Số ô logic phải được tính bằng `boardWidth * boardHeight`.
- Phân biệt rõ số **điểm tọa độ** và số **ô**. Nếu có 50×50 điểm thì board generator có 49×49 khoảng chia; nếu có 50×50 ô thì cần 51×51 điểm.
- Model cũ chỉ tương thích với đúng schema cũ. Với baseline hiện tại, `31 x 19 = 589`; đổi sang kích thước khác phải tạo schema/model mới.
- Lưu `boardWidth`, `boardHeight`, `winLength`, `observationSize` và `actionSize` trong metadata của mỗi model.
- Xác nhận và ghi rõ luật khi có chuỗi dài hơn 5: vẫn thắng khi `>= 5` hay bắt buộc đúng 5.
- AI không được trả về ô đã có quân, ô ngoài bàn hoặc nước đi không thể thực thi bởi `GameManager`.
- Không ghi đè model, checkpoint hoặc baseline đang phát hành.
- Mọi thay đổi làm tăng observation/action phải tăng `schemaVersion` và train model mới.
- Mọi benchmark phải tách kết quả khi AI đi trước và đi sau.

## 4. Cấu hình kích thước bàn cờ

Tạo một nguồn cấu hình dùng chung, ưu tiên:

```text
Assets/Scripts/ScriptOfAI/CaroBoardConfig.cs
```

Cấu hình tối thiểu:

```text
boardPointsX
boardPointsY
winLength
cellSize
```

Quy ước:

| Mục tiêu | Điểm X/Y | Khoảng chia Generator/Visual/Camera | Observation/Action |
|---|---:|---:|---:|
| Baseline cũ | 31×19 | 30×18 | 589 |
| Bàn 50×50 điểm | 50×50 | 49×49 | 2.500 |
| Bàn 100×100 điểm | 100×100 | 99×99 | 10.000 |

- [ ] `GameManager` lấy kích thước từ `CaroBoardConfig`, không giữ `const 31/19`.
- [ ] `BoardGenerator` và `GameVisualManager` tính số khoảng chia từ số điểm, không tự giữ một bộ số khác.
- [ ] `CaroCameraController` lấy cùng kích thước và tự tính bounds/pan/zoom.
- [ ] `MatchRules`, `CaroAiBoard`, Search và candidate generator nhận kích thước qua config.
- [ ] Training scene và runtime Behavior Parameters dùng cùng `observationSize` và `actionSize`.
- [ ] Nếu board từ 50×50 trở lên, không tạo một GameObject click riêng cho từng điểm; dùng collider/raycast và đổi vị trí thành tọa độ lưới.
- [ ] Với 100×100, dùng candidate set thưa/incremental thay vì quét và cấp phát toàn bộ 10.000 ô ở mỗi node search.

Model cũ `589` chỉ được giữ cho baseline. Không cố gắn model này vào bàn 50×50 hoặc 100×100.

## 5. Cấu hình mục tiêu theo độ khó

Các thông số dưới đây là giá trị khởi điểm, phải hiệu chỉnh bằng benchmark trên máy thật.

| Độ khó | Logic chính | Độ sâu | Ngân sách PC | Ngân sách Mobile |
|---|---|---:|---:|---:|
| Normal | Heuristic + Alpha-Beta cơ bản | 2–3 ply | 80–150 ms | 50–100 ms |
| Hard | Tactical + Alpha-Beta + safety | 4–5 ply | 200–400 ms | 100–250 ms |
| Extreme | Tactical + Search + ML policy | 5–7 ply | 500–1.000 ms | 250–500 ms |

Độ khó phải được cấu hình bằng `CaroDifficultySettings`, không hard-code trong `CaroSearchGameplayAgent`.

## 6. Thứ tự triển khai bắt buộc

### Milestone 0 – Khóa baseline và tiêu chí đánh giá

Mục tiêu: biết chính xác AI hiện tại mạnh đến đâu trước khi sửa.

- [ ] Lưu bản sao model ONNX hiện tại, checkpoint, config và commit đang dùng.
- [ ] Ghi hash SHA-256, ngày tạo và phiên bản của model baseline.
- [ ] Ghi rõ runtime hiện tại là Alpha-Beta thuần khi `useSearchAI = true`.
- [ ] Tạo evaluation runner chạy không cần UI.
- [ ] Ghi `WinRate`, `LossRate`, `DrawRate`, lượt đi trước/sau.
- [ ] Ghi thời gian suy nghĩ trung bình, p95, tối đa và số node search.
- [ ] Lưu seed, opening và kết quả để tái lập.
- [ ] Tạo bộ thế cờ cố định gồm thắng ngay, chặn ngay và bẫy 2–4 ply.
- [ ] Chốt profile kích thước cần hỗ trợ: baseline, 50×50 điểm và 100×100 điểm.
- [ ] Ghi riêng benchmark theo từng kích thước, không gộp kết quả các board.

Điều kiện hoàn thành:

- Có baseline tối thiểu 500 ván cho mỗi loại đối thủ ở vòng đầu.
- Chạy lại cùng seed cho kết quả tái lập được.
- Chưa thay model phát hành trước khi có báo cáo baseline.

### Milestone 1 – Chuẩn hóa Board và Rules

Mục tiêu: training, search và gameplay không còn tự duy trì các bản sao luật khác nhau.

- [ ] Dùng `CaroAiBoard` làm trạng thái dữ liệu thuần hoặc đổi tên thành `CaroBoardState`.
- [ ] Tạo `CaroBoardConfig` làm nguồn duy nhất cho chiều rộng, chiều cao và `winLength`.
- [ ] Loại bỏ các hằng số kích thước riêng trong `GameManager` và `CaroAgent`.
- [ ] Đưa kiểm tra thắng, hòa, biên bàn và `winLength` vào một lớp luật dùng chung.
- [ ] Tạo adapter giữa `GameManager.PlayerType` và giá trị `1/-1/0` của AI.
- [ ] Đồng bộ `GameManager`, `CaroGameplayAgent`, `CaroAgent` và Search với cùng encoder.
- [ ] Có `MakeMove`/`UndoMove` an toàn cho search.
- [ ] Không để lớp ngoài sửa trực tiếp mảng `Cells`.

Test bắt buộc:

- Ngang, dọc, hai đường chéo.
- Sát biên và góc bàn.
- Chuỗi dài hơn 5.
- Hòa toàn bàn.
- Nước đi không hợp lệ.
- Reset/rematch không giữ lại trạng thái cũ.

### Milestone 2 – Tactical Scanner

Mục tiêu: Hard không bỏ qua nước thắng hoặc nước chặn bắt buộc.

Tạo `CaroThreatAnalyzer` với các kết quả rõ ràng:

- [ ] Nước thắng ngay của AI.
- [ ] Tất cả nước thắng ngay của đối thủ.
- [ ] Bốn kín, bốn hở, ba hở.
- [ ] Chuỗi khuyết như `XX_XX`.
- [ ] Song ba, song bốn và đe dọa kép.
- [ ] Nước tạo chuỗi ép buộc trong lượt kế tiếp.
- [ ] Nước phải giữ lại dù vượt quá `maxCandidates`.

Thứ tự ưu tiên:

1. Thắng ngay.
2. Chặn tất cả nước thua ngay nếu có thể.
3. Thoát khỏi đe dọa kép hoặc tạo thắng bắt buộc.
4. Tạo/chặn bốn hở.
5. Tạo/chặn ba hở.
6. Chuyển các nước còn lại cho Search.

Điều kiện hoàn thành:

- Tactical test pass 100% trên bộ thế cờ cố định.
- Scanner không thay đổi board.
- Có test giữa bàn và sát biên.
- Không cắt mất nước bắt buộc khi giới hạn số ứng viên.

### Milestone 3 – Củng cố Alpha-Beta cho Hard

Mục tiêu: Search tìm được bẫy nhiều lượt mà vẫn giữ ngân sách thời gian.

- [ ] Sửa trường hợp đối thủ có nhiều nước thắng: không chỉ tìm trong các ô cần chặn; phải đánh giá cả nước phản công hoặc nước thắng của AI.
- [ ] Kiểm tra terminal state sau mỗi nước đi, kể cả thắng, thua và hòa.
- [ ] Giữ iterative deepening nhưng chỉ nhận kết quả của depth hoàn tất.
- [ ] Thêm Zobrist hash và Transposition Table.
- [ ] Thêm move ordering: nước thắng, nước chặn, tactical score, killer move và history score.
- [ ] Không cấp phát `List`, `bool[]` và object mới ở mỗi node nếu có thể tái sử dụng buffer.
- [ ] Bảo đảm timeout không trả về nước chưa được kiểm tra.
- [ ] Profile trên PC cấu hình thấp và thiết bị Mobile thật.
- [ ] Benchmark riêng cho 31×19, 50×50 và 100×100; không dùng cùng depth/time budget một cách mù quáng.
- [ ] Nếu cần chạy nền, chỉ chạy phần board/search thuần; không truy cập Unity API từ worker.

Điều kiện hoàn thành:

- Search luôn trả về nước hợp lệ khi còn ô trống.
- Giải được bộ bẫy tối thiểu 2–4 ply.
- p95 không vượt ngân sách độ khó đã cấu hình.
- Không làm treo hoặc giật rõ rệt Game View.

### Milestone 4 – Tách cấu hình Normal/Hard/Extreme và nối runtime

Mục tiêu: người chơi chọn độ khó thật sự, không chỉ đổi tên UI.

Tạo các file:

```text
Assets/Scripts/ScriptOfAI/CaroDifficultySettings.cs
Assets/Scripts/ScriptOfAI/CaroMoveSelector.cs
Assets/Scripts/ScriptOfAI/CaroDifficultyPreset.cs
```

- [ ] Tạo preset Normal, Hard và Extreme.
- [ ] Cho phép chọn board profile độc lập với độ khó.
- [ ] Cho phép đặt depth, time budget, radius, max candidates và bật/tắt ML.
- [ ] Có ngân sách riêng cho PC và Mobile.
- [ ] `NetworkManagerUI` chỉ gọi một điểm vào là `CaroMoveSelector`.
- [ ] Log nguồn nước đi: `Tactical`, `Search`, `ML` hoặc `Fallback`.
- [ ] Kiểm tra mỗi lượt chỉ gọi AI một lần.
- [ ] Không tạo nhiều `CaroSearchGameplayAgent` sau rematch hoặc đổi scene.
- [ ] Fallback về nước hợp lệ nhanh nhất nếu timeout hoặc model lỗi.

Định nghĩa runtime:

- Normal: Search nông, không cần model.
- Hard: Tactical Scanner + Alpha-Beta là logic chính, không bắt buộc train lại.
- Extreme: thêm ML policy để xếp thứ tự ứng viên và Search xác nhận nước cuối.

### Milestone 5 – Test và Evaluation Gate cho Hard

Mục tiêu: xác nhận Hard mạnh hơn Normal bằng số liệu, không đánh giá bằng cảm giác.

- [ ] EditMode tests cho Board, Rules, Tactical và Search.
- [ ] PlayMode smoke test cho Single Play, rematch, đổi scene và game over.
- [ ] Chạy benchmark với Random, Heuristic, Normal và baseline model.
- [ ] Tách lượt đi trước/sau.
- [ ] Đo tỷ lệ thắng ngay bị bỏ lỡ.
- [ ] Đo tỷ lệ chặn immediate threat.
- [ ] Đo nước không hợp lệ.
- [ ] Đo thời gian trung bình, p95, tối đa và số node.
- [ ] Đo độ ổn định trên PC và Mobile.

Evaluation gate Hard:

- 100% nước thắng ngay trong tactical suite.
- 100% chặn được đe dọa thắng đơn có thể chặn.
- 0 nước đi không hợp lệ trong benchmark.
- Không vượt time budget đã cấu hình.
- Kết quả thắng trước Normal/heuristic phải vượt baseline theo ngưỡng được ghi trong báo cáo M0.

### Milestone 6 – Chuẩn bị ML-Agent v2, chưa train ngay

Mục tiêu: chỉ dùng ML khi nó đem lại lợi ích đo được cho Search.

- [ ] Giữ model v1 và observation/action schema hiện tại để làm baseline.
- [ ] Thêm ML policy vào `CaroMoveSelector` trước, chỉ dùng để xếp thứ tự ứng viên.
- [ ] Không cho ML bỏ qua nước thắng hoặc nước chặn do Tactical Scanner phát hiện.
- [ ] Ghi riêng kết quả của ML, Search và Hybrid.
- [ ] Chỉ thay observation khi Hard benchmark đã ổn định.
- [ ] Nếu nâng observation, dùng plane quân mình, quân đối thủ, ô trống, legal mask và threat feature.
- [ ] Tạo `schemaVersion` trong metadata model.

### Milestone 7 – Self-play và curriculum cho Extreme

Chỉ bắt đầu khi Milestone 5 đã pass.

- [ ] Tạo môi trường hai Agent dùng chung luật.
- [ ] Dùng Team ID khác nhau và reward zero-sum `+1/-1`.
- [ ] Luân phiên bên đi trước sau mỗi episode.
- [ ] Dùng snapshot cũ làm đối thủ, không chỉ đánh với model mới nhất.
- [ ] Có random opening và tactical position replay.
- [ ] Theo dõi Elo, entropy, episode length, win/draw/loss và reward.
- [ ] Giới hạn tổng shaping reward mỗi ván gần `[-0.1, +0.1]`.
- [ ] Giảm dần reward phụ về gần 0.
- [ ] Tạo riêng `config/caro_selfplay.yaml`, không ghi đè `config/caro.yaml`.

Curriculum đề xuất:

```text
Giai đoạn A: Random + Heuristic + Tactical opponent
Giai đoạn B: Latest model + Snapshot league
Giai đoạn C: Search opponent + Tactical replay + giảm shaping
```

### Milestone 8 – Phát hành Extreme

- [ ] Chạy evaluation matrix với Random, Heuristic, Tactical, baseline, snapshot và Search.
- [ ] Chạy tối thiểu 1.000 ván cho mỗi đối thủ quan trọng.
- [ ] Tính Elo và khoảng tin cậy.
- [ ] Kiểm tra đi trước và đi sau.
- [ ] Profile PC/Mobile.
- [ ] Lưu run ID, checkpoint step, hash model, config, metrics và commit.
- [ ] Chỉ copy model đã qua gate vào `Assets/Models`.
- [ ] Chạy PlayMode smoke test trước khi phát hành.
- [ ] Sao lưu model cũ và có khả năng rollback.

## 7. Cấu trúc file sẽ bổ sung

```text
Assets/Scripts/ScriptOfAI/
├── CaroBoardConfig.cs             # Kích thước board và winLength dùng chung
├── CaroAiBoard.cs                 # Trạng thái board thuần
├── CaroAiRules.cs                 # Luật dùng chung
├── CaroAiMoveGenerator.cs         # Sinh ứng viên
├── CaroAiEvaluator.cs             # Đánh giá nền
├── CaroThreatAnalyzer.cs          # Phân tích chiến thuật
├── CaroAlphaBetaSearch.cs         # Search
├── CaroTranspositionTable.cs      # Cache trạng thái
├── CaroZobristHash.cs             # Hash bàn cờ
├── CaroDifficultySettings.cs      # Cấu hình độ khó
├── CaroMoveSelector.cs            # Điểm vào runtime duy nhất
├── CaroSearchGameplayAgent.cs     # Adapter Unity hiện tại
└── CaroEvaluationRunner.cs        # Chạy benchmark

Assets/Tests/EditMode/CaroAI/
├── CaroAiRulesTests.cs
├── CaroThreatAnalyzerTests.cs
├── CaroSearchTests.cs
└── CaroDifficultyTests.cs

config/
├── caro.yaml                      # Baseline/v1
└── caro_selfplay.yaml             # Extreme/self-play
```

## 8. Các việc chưa được phép làm sớm

- Không train lại model chỉ vì AI chơi chưa đủ khó khi Tactical/Search chưa có benchmark.
- Không thay observation size khi chưa tăng schema version.
- Không đổi kích thước board trong Scene mà không đổi `GameManager`, training scene, camera và visual manager cùng lúc.
- Không dùng model 589 cho board có observation/action khác 589.
- Không copy checkpoint mới vào `Assets/Models` trước evaluation gate.
- Không tăng depth vô hạn để đổi lấy độ khó vì sẽ gây giật Mobile.
- Không để `GameManager`, training và search có ba cách kiểm tra thắng khác nhau.
- Không dùng cumulative reward làm tiêu chí duy nhất.

## 9. Definition of Done

### Hard hoàn thành khi

- Tactical suite đạt 100% nước thắng/chặn bắt buộc.
- Search giải được bẫy 2–4 ply.
- 0 nước không hợp lệ trong benchmark.
- p95 thời gian nằm trong ngân sách PC/Mobile.
- Single Play, rematch, game over và đổi scene không tạo callback trùng.
- Có báo cáo so sánh Normal và Hard.
- Có benchmark riêng cho từng board profile được hỗ trợ.

### Extreme hoàn thành khi

- Hard gate đã pass.
- Self-play và snapshot league hoạt động đúng.
- Model hybrid vượt baseline ở benchmark cố định.
- Có Elo, confidence interval, metadata và rollback model.

## 10. Thứ tự thực hiện cuối cùng

1. Tạo `CaroBoardConfig` và chốt profile 31×19, 50×50 hoặc 100×100 điểm.
2. Khóa baseline và tạo evaluation runner theo từng board profile.
3. Chuẩn hóa Board/Rules giữa training, search, gameplay, camera và visual.
4. Tối ưu cách sinh điểm/click để không tạo hàng nghìn GameObject trên board lớn.
5. Viết Tactical Scanner và test toàn bộ pattern.
6. Sửa và tối ưu Alpha-Beta cho Hard.
7. Tạo cấu hình Normal/Hard/Extreme và nối `CaroMoveSelector`.
8. Chạy benchmark PC/Mobile theo từng kích thước và hoàn tất Hard.
9. Tích hợp ML policy vào hybrid; nếu đổi board size thì train schema/model tương ứng.
10. Tạo self-play, snapshot league và curriculum.
11. Train model Extreme cho từng board profile cần phát hành.
12. Chạy evaluation gate và phát hành model có rollback.

## 11. Tiến độ triển khai Hard (13/11/2026)

- Đã tạo `CaroDifficultySettings`: preset Normal và Hard, ngân sách riêng PC/Mobile.
- Đã tạo `CaroThreatAnalyzer`: tìm nước thắng tức thì và chấm chuỗi có ô khuyết theo bốn hướng.
- Đã sửa search để giữ nước chặn khi giới hạn ứng viên và xét nước phản công nếu đối thủ có nhiều ô thắng.
- Single Play mặc định chọn Hard trong `NetworkManagerUI`; có thể đổi thành Normal trong Inspector.
- Đã kiểm tra biên dịch C# và smoke test cho thắng ngay, chặn ngay và chuỗi khuyết.
- Đã bổ sung ưu tiên chặn chuỗi ba hở cho Hard khi không có nước thắng tức thì; kiểm thử thế cờ ba hở đấu với chuỗi hai đã qua.
- Search chạy trên bản sao bàn cờ ở luồng nền; trước khi đặt quân sẽ kiểm tra lại lượt và phiên bản bàn cờ để bỏ kết quả cũ sau rematch.
- Cần đo frame time bằng Profiler khi PlayMode trên PC/Mobile để xác nhận hiện tượng animation khựng đã hết.
- Chưa hoàn thành Tactical Scanner đầy đủ: bốn hở/bốn kín, song ba/song bốn và threat-space search cần test cố định.
- Chưa hoàn thành Hard gate: thiếu evaluation runner, benchmark PC/Mobile, kiểm thử PlayMode và tối ưu cho board 50×50/100×100.
- Chưa chuyển kích thước board sang cấu hình động; runtime vẫn dùng 31×19 cho đến Milestone 1.

Trạng thái: **Hard bản đầu đã nối vào Single Play, chưa được xác nhận đạt chuẩn Hard theo evaluation gate.**

