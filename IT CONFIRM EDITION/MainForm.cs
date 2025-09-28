using IT_CONFIRM_EDITION.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IT_CONFIRM_EDTION
{
    public partial class MainForm : Form
    {
        #region KHAI BÁO CÁC BIẾN
        private TextBox currentTextBox;
        private readonly ToolTip validationToolTip;
        private string _lastSavedFilePath; // Biến mới để lưu đường dẫn file        
        private readonly ToolTip statusToolTip;// Biến mới cho ToolTip của thông báo trạng thái     
        private readonly Timer rainbowTimer;// Các biến cho hiệu ứng chuyển màu cầu vồng mượt mà
        private bool isRainbowActive = false;
        private Color originalCopyrightColor;
        private double rainbowPhase = 0;       
        private readonly Dictionary<string, Color> originalColors = new Dictionary<string, Color>();// Sử dụng Dictionary để lưu màu gốc của tất cả các nút
        private RadioButton rdoI251; // Khai báo RadioButton cho I251
        private RadioButton rdoI252; // Khai báo RadioButton cho I252

        // Biến cho NAS
        private string nasFilePath; // Không phải readonly, cho phép gán trong SetFilePath
        private string nasDirectoryPath; // readonly, chỉ gán trong constructor

        //Để tính PPI cho màn hình 11 inch với độ phân giải 1668x2420:
        //Chiều dài cell: 2420/232 = 10.43 , Chiều cao cell: 1668/160 = 10.43
        //private const double MmToPixelRatio = 10.43; // Tỷ lệ chuyển đổi từ mm sang pixel cho màn hình 1668x2420, 11 inch

        //Để tính PPI cho màn hình 13 inch với độ phân giải 2048x2732:
        //Chiều dài cell: 2732/264 = 10.34 , Chiều cao cell: 2048/193 = 10.34
        //private const double MmToPixelRatio = 10.???; // Tỷ lệ chuyển đổi từ mm sang pixel cho màn hình 2048x2732, 13 inch

        // Tỷ lệ chuyển đổi từ mm sang pixel, sẽ được cập nhật dựa trên model được chọn
        private double MmToPixelRatio => rdoI251.Checked ? 10.43 : 10.34; // I251: 10.43, I252: 10.34      
        #endregion

        #region FORM KHỞI TẠO UI
        public MainForm()
        {
            InitializeComponent();
            string eqpid = ReadEQPIDFromIniFile();
            this.Text = "IT CONFIRM EDITION" + (string.IsNullOrEmpty(eqpid) ? "" : "_" + eqpid + "");
            InitializeKeyboardEvents();
            TxtSAPN.MaxLength = 300;
            validationToolTip = new ToolTip();//Thông báo yêu cầu nhập dữ liệu
            statusToolTip = new ToolTip();//Gợi ý bấm vào để mở thư mục
            this.LblStatus.Text = "Sẵn sàng nhập dữ liệu...";
            UpdateSavedSAPNCount();

            // Khởi tạo ComboBox với danh sách lỗi
            cboErrorType.Items.AddRange(new string[] { "B-SPOT", "WHITE SPOT", "ĐỐM SPIN", "ĐỐM PANEL", "ĐỐM ĐƯỜNG DỌC", "-" });
            cboErrorType.SelectedIndex = -1; // Mặc định chọn "ĐỐM" =-1 KHÔNG CHỌN GÌ

            // Khởi tạo timer cho hiệu ứng cầu vồng
            this.rainbowTimer = new Timer
            {
                Interval = 20 // Cập nhật màu mỗi 20ms để mượt hơn
            };
            this.rainbowTimer.Tick += new EventHandler(this.RainbowTimer_Tick);

            // Gắn sự kiện cho LblCopyright
            LblCopyright.MouseEnter += LblCopyright_MouseEnter;
            LblCopyright.MouseLeave += LblCopyright_MouseLeave;
            // Gán sự kiện Click và thay đổi con trỏ chuột cho LblStatus
            this.LblStatus.Click += new EventHandler(this.LblStatus_Click);
            this.LblStatus.Cursor = Cursors.Hand;

            // Khởi tạo NAS
            var nasCredentials = ReadNASCredentialsFromIniFile(); // Gọi hàm để đọc credentials và tạo file NAS.ini       
            // Tạo đường dẫn file dựa trên ngày hiện tại
            SetFilePath();

            // Khởi tạo hiệu ứng cho các nút
            InitializeButtonEffects();
        }
        #endregion

        #region HIỆU ỨNG CHO CÁC NÚT BẤM
        private void InitializeButtonEffects()
        {
            Color keyboardBaseColor = System.Drawing.ColorTranslator.FromHtml("#FFF");

            // Xử lý nút SAVE (giữ nguyên màu ban đầu)
            originalColors.Add("BtnSave", BtnSave.BackColor);
            BtnSave.MouseEnter += Button_MouseEnter;
            BtnSave.MouseLeave += Button_MouseLeave;
            BtnSave.MouseDown += Button_MouseDown;
            BtnSave.MouseUp += Button_MouseUp;

            // Xử lý nút RESET (giữ nguyên màu ban đầu)
            originalColors.Add("BtnReset", BtnReset.BackColor);
            BtnReset.MouseEnter += Button_MouseEnter;
            BtnReset.MouseLeave += Button_MouseLeave;
            BtnReset.MouseDown += Button_MouseDown;
            BtnReset.MouseUp += Button_MouseUp;

            // Đặt lại màu cho nút ALL và xử lý riêng biệt
            BtnAll.BackColor = System.Drawing.ColorTranslator.FromHtml("#97FFFF");
            originalColors.Add("BtnAll", BtnAll.BackColor);
            BtnAll.MouseEnter += Button_MouseEnter;
            BtnAll.MouseLeave += Button_MouseLeave;
            BtnAll.MouseDown += Button_MouseDown;
            BtnAll.MouseUp += Button_MouseUp;

            // Áp dụng màu nền mới cho các nút trên bàn phím ảo (trừ nút ALL và BtnBack)
            Button[] keyboardButtons = { Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9, BtnBack };
            foreach (Button Btn in keyboardButtons)
            {
                // Chỉ áp dụng màu nền mới cho các nút số
                if (Btn.Name != "BtnBack")
                {
                    Btn.BackColor = keyboardBaseColor;
                }

                // Lưu màu nền hiện tại (đã được đổi) vào từ điển
                originalColors.Add(Btn.Name, Btn.BackColor);

                Btn.MouseEnter += Button_MouseEnter;
                Btn.MouseLeave += Button_MouseLeave;
                Btn.MouseDown += Button_MouseDown;
                Btn.MouseUp += Button_MouseUp;
            }
        }

        // Hiệu ứng khi di chuột vào nút
        private void Button_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Button Btn)
            {
                // Áp dụng hiệu ứng di chuột cho TẤT CẢ các nút
                if (originalColors.ContainsKey(Btn.Name))
                {
                    Color originalColor = originalColors[Btn.Name];
                    int r = Math.Min(255, originalColor.R + 30);
                    int g = Math.Min(255, originalColor.G + 30);
                    int b = Math.Min(255, originalColor.B + 30);
                    Btn.BackColor = Color.FromArgb(r, g, b);
                }
            }
        }

        private void Button_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Button Btn && originalColors.ContainsKey(Btn.Name))
            {
                // Khôi phục màu nền ban đầu
                Btn.BackColor = originalColors[Btn.Name];
            }
        }

        private void Button_MouseDown(object sender, EventArgs e)
        {
            if (sender is Button Btn)
            {
                // Áp dụng hiệu ứng khi nhấn chuột xuống cho TẤT CẢ các nút
                if (originalColors.ContainsKey(Btn.Name))
                {
                    Color originalColor = originalColors[Btn.Name];
                    int r = Math.Max(0, originalColor.R - 30);
                    int g = Math.Max(0, originalColor.G - 30);
                    int b = Math.Max(0, originalColor.B - 30);
                    Btn.BackColor = Color.FromArgb(r, g, b);
                }
            }
        }

        private void Button_MouseUp(object sender, EventArgs e)
        {
            if (sender is Button Btn)
            {
                if (Btn.ClientRectangle.Contains(Btn.PointToClient(Cursor.Position)))
                {
                    // Nếu nhả chuột trong vùng nút, khôi phục hiệu ứng di chuột vào
                    if (originalColors.ContainsKey(Btn.Name))
                    {
                        Color originalColor = originalColors[Btn.Name];
                        int r = Math.Min(255, originalColor.R + 30);
                        int g = Math.Min(255, originalColor.G + 30);
                        int b = Math.Min(255, originalColor.B + 30);
                        Btn.BackColor = Color.FromArgb(r, g, b);
                    }
                }
                else
                {
                    // Nếu nhả chuột ngoài vùng nút, khôi phục màu ban đầu
                    if (originalColors.ContainsKey(Btn.Name))
                    {
                        Btn.BackColor = originalColors[Btn.Name];
                    }
                }
            }
        }
        #endregion

        #region XỬ LÝ BÀN PHÍM ẢO VÀ KIỂM TRA DỮ LIỆU
        // Hàm này được gọi trong constructor
        private void InitializeKeyboardEvents()
        {
            // Gán sự kiện Click để xác định TextBox hiện tại
            TxtSAPN.Click += TextBox_Click;
            TxtSx1.Click += TextBox_Click;
            TxtSy1.Click += TextBox_Click;
            TxtEx1.Click += TextBox_Click;
            TxtEy1.Click += TextBox_Click;
            TxtSx2.Click += TextBox_Click;
            TxtSy2.Click += TextBox_Click;
            TxtEx2.Click += TextBox_Click;
            TxtEy2.Click += TextBox_Click;
            TxtSx3.Click += TextBox_Click;
            TxtSy3.Click += TextBox_Click;
            TxtEx3.Click += TextBox_Click;
            TxtEy3.Click += TextBox_Click;
            TxtX1.Click += TextBox_Click;
            TxtY1.Click += TextBox_Click;
            TxtX2.Click += TextBox_Click;
            TxtY2.Click += TextBox_Click;
            TxtX3.Click += TextBox_Click;
            TxtY3.Click += TextBox_Click;

            // Gán sự kiện KeyDown cho TxtSAPN
            TxtSAPN.KeyDown += TxtSAPN_KeyDown;

            // Gán sự kiện KeyPress riêng cho TxtSx1 (cho phép ALL)
            TxtSx1.KeyPress += TxtSx1_KeyPress;

            // Gán sự kiện KeyPress chung cho các ô tọa độ còn lại
            TxtSy1.KeyPress += CoordinateTextBox_KeyPress;
            TxtEx1.KeyPress += CoordinateTextBox_KeyPress;
            TxtEy1.KeyPress += CoordinateTextBox_KeyPress;
            TxtSx2.KeyPress += CoordinateTextBox_KeyPress;
            TxtSy2.KeyPress += CoordinateTextBox_KeyPress;
            TxtEx2.KeyPress += CoordinateTextBox_KeyPress;
            TxtEy2.KeyPress += CoordinateTextBox_KeyPress;
            TxtSx3.KeyPress += CoordinateTextBox_KeyPress;
            TxtSy3.KeyPress += CoordinateTextBox_KeyPress;
            TxtEx3.KeyPress += CoordinateTextBox_KeyPress;
            TxtEy3.KeyPress += CoordinateTextBox_KeyPress;
            TxtX1.KeyPress += CoordinateTextBox_KeyPress;
            TxtY1.KeyPress += CoordinateTextBox_KeyPress;
            TxtX2.KeyPress += CoordinateTextBox_KeyPress;
            TxtY2.KeyPress += CoordinateTextBox_KeyPress;
            TxtX3.KeyPress += CoordinateTextBox_KeyPress;
            TxtY3.KeyPress += CoordinateTextBox_KeyPress;
        }

        // Khi chuột di vào nhãn, kích hoạt hiệu ứng cầu vồng
        private void LblCopyright_MouseEnter(object sender, EventArgs e)
        {
            if (!isRainbowActive)
            {
                isRainbowActive = true;
                originalCopyrightColor = LblCopyright.ForeColor;
                rainbowTimer.Start();
            }
        }

        // Khi chuột rời nhãn, tắt hiệu ứng và khôi phục màu gốc
        private void LblCopyright_MouseLeave(object sender, EventArgs e)
        {
            if (isRainbowActive)
            {
                isRainbowActive = false;
                rainbowTimer.Stop();
                LblCopyright.ForeColor = originalCopyrightColor;
            }
        }
        #endregion

        #region THAY ĐỔI MÀU SẮC KHI DI CHUỘT VÀO TÊN TÁC GIẢ
        // Sự kiện Tick của timer, cập nhật màu sắc
        private void RainbowTimer_Tick(object sender, EventArgs e)
        {
            rainbowPhase += 0.05; // Giảm tốc độ thay đổi để màu chuyển từ từ hơn

            Color newColor = CalculateRainbowColor(rainbowPhase);
            LblCopyright.ForeColor = newColor;
        }

        // Tính toán màu sắc cầu vồng dựa trên giai đoạn
        private Color CalculateRainbowColor(double phase)
        {
            double red = Math.Sin(phase) * 127 + 128;
            double green = Math.Sin(phase + 2 * Math.PI / 3) * 127 + 128;
            double blue = Math.Sin(phase + 4 * Math.PI / 3) * 127 + 128;

            red = Math.Max(0, Math.Min(255, red));
            green = Math.Max(0, Math.Min(255, green));
            blue = Math.Max(0, Math.Min(255, blue));

            return Color.FromArgb((int)red, (int)green, (int)blue);
        }
        private void TextBox_Click(object sender, EventArgs e)
        {
            currentTextBox = (TextBox)sender;
            currentTextBox.Focus(); // Đảm bảo TextBox được focus
        }
        #endregion

        #region KIỂM TRA ĐÃ NHẬP DỮ LIÊU HAY CHƯA
        // Phương thức xử lý sự kiện Click cho LblStatus
        private void LblStatus_Click(object sender, EventArgs e)
        {
            // Kiểm tra xem đã có đường dẫn file được lưu chưa
            if (!string.IsNullOrEmpty(_lastSavedFilePath))
            {
                try
                {
                    // Lấy đường dẫn thư mục chứa file
                    string folderPath = Path.GetDirectoryName(_lastSavedFilePath);
                    // Mở thư mục bằng Windows Explorer
                    Process.Start("explorer.exe", folderPath);
                }
                catch (Exception ex)
                {
                    // Báo lỗi nếu không thể mở thư mục
                    MessageBox.Show($"Không thể mở thư mục. Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Phương thức xử lý sự kiện KeyDown của TxtSAPN
        private void TxtSAPN_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                TxtSx1.Focus();
                currentTextBox = TxtSx1; // Cập nhật currentTextBox để bàn phím ảo tương tác với TxtSx1
                e.SuppressKeyPress = true;
            }
        }

        // Xử lý các nút số trên bàn phím ảo
        private void BtnNumber_Click(object sender, EventArgs e)
        {
            if (currentTextBox != null)
            {
                Button Btn = (Button)sender;
                string buttonText = Btn.Text;

                // Kiểm tra nếu ô nhập đã đạt giới hạn 3 ký tự
                if (currentTextBox.Text.Length >= 3 && !buttonText.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // Trừ trường hợp người dùng xóa hoặc nhập "ALL"
                    validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                    validationToolTip.Show("Chỉ cho phép nhập tối đa 3 số", currentTextBox, 0, currentTextBox.Height, 2000);
                    return;
                }

                // Chỉ cho phép nhập "All" vào TxtSx1
                if (buttonText.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentTextBox != TxtSx1 || !string.IsNullOrWhiteSpace(currentTextBox.Text))
                    {
                        // Hiển thị tooltip nếu cố gắng nhập "ALL" vào ô khác
                        if (currentTextBox != TxtSx1)
                        {
                            validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                            validationToolTip.Show("Chỉ cho phép nhập ALL vào ô Sx1", currentTextBox, 0, currentTextBox.Height, 2000);
                        }
                        return; // Không làm gì nếu không phải TxtSx1 hoặc ô đã có nội dung
                    }
                    currentTextBox.Text = buttonText;
                }
                // Cho phép nhập số vào các ô
                else
                {
                    currentTextBox.Text += buttonText;
                }
            }
        }

        private void TxtSx1_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox currentTextBox = (TextBox)sender;
            string currentText = currentTextBox.Text;

            // Nếu người dùng nhập "ALL" và muốn thêm ký tự, hãy ngăn chặn
            if (currentText.ToUpper() == "ALL" && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                validationToolTip.Show("Không thể nhập thêm sau 'ALL'", currentTextBox, 0, currentTextBox.Height, 2000);
                return;
            }

            // Kiểm tra giới hạn 3 ký tự
            if (!char.IsControl(e.KeyChar) && currentText.Length >= 3)
            {
                // Nếu ký tự mới là số và độ dài đã đạt 3, ngăn chặn
                if (char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                    validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                    validationToolTip.Show("Chỉ cho phép nhập tối đa 3 số", currentTextBox, 0, currentTextBox.Height, 2000);
                    return;
                }
            }

            // Cho phép nhập số, Backspace, và các ký tự 'a', 'A', 'l', 'L'
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && "ALL".IndexOf(char.ToUpper(e.KeyChar)) < 0)
            {
                e.Handled = true;
                validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                validationToolTip.Show("Chỉ cho phép nhập số hoặc ALL", currentTextBox, 0, currentTextBox.Height, 2000);
            }
        }

        // Xử lý nút xóa (Back)
        private void BtnBack_Click(object sender, EventArgs e)
        {
            if (currentTextBox != null && currentTextBox.Text.Length > 0)
            {
                currentTextBox.Text = currentTextBox.Text.Substring(0, currentTextBox.Text.Length - 1);
            }
        }

        // Kiểm tra dữ liệu sAPN và ít nhất một trường tọa độ
        private bool IsDataValid()
        {
            if (string.IsNullOrWhiteSpace(TxtSAPN.Text))
            {
                return false;
            }

            // Kiểm tra xem ít nhất một trong các ô tọa độ có dữ liệu không
            if (string.IsNullOrWhiteSpace(TxtSx1.Text) && string.IsNullOrWhiteSpace(TxtSy1.Text) && string.IsNullOrWhiteSpace(TxtEx1.Text) && string.IsNullOrWhiteSpace(TxtEy1.Text) &&
                string.IsNullOrWhiteSpace(TxtSx2.Text) && string.IsNullOrWhiteSpace(TxtSy2.Text) && string.IsNullOrWhiteSpace(TxtEx2.Text) && string.IsNullOrWhiteSpace(TxtEy2.Text) &&
                string.IsNullOrWhiteSpace(TxtSx3.Text) && string.IsNullOrWhiteSpace(TxtSy3.Text) && string.IsNullOrWhiteSpace(TxtEx3.Text) && string.IsNullOrWhiteSpace(TxtEy3.Text) &&
                string.IsNullOrWhiteSpace(TxtX1.Text) && string.IsNullOrWhiteSpace(TxtY1.Text) && string.IsNullOrWhiteSpace(TxtX2.Text) && string.IsNullOrWhiteSpace(TxtY2.Text) &&
                string.IsNullOrWhiteSpace(TxtX3.Text) && string.IsNullOrWhiteSpace(TxtY3.Text))
            {
                return false;
            }
            return true;
        }

        #endregion

        #region SƯ KIỆN BẤM NÚT SAVE VÀ RESET
        // Xử lý nút SAVE
        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Kiểm tra xem đã chọn loại lỗi chưa
            if (cboErrorType.SelectedIndex == -1)
            {
                validationToolTip.ToolTipIcon = ToolTipIcon.Warning;
                validationToolTip.ToolTipTitle = "Lỗi";
                validationToolTip.Show("Vui lòng chọn loại lỗi!", cboErrorType, 0, cboErrorType.Height, 5000);
                return;
            }

            //Kiểm tra xem đã chọn model chưa
            if (!rdoI251.Checked && !rdoI252.Checked)
            {
                validationToolTip.ToolTipIcon = ToolTipIcon.Warning;
                validationToolTip.ToolTipTitle = "Lỗi";
                validationToolTip.Show("Vui lòng chọn model (I251 hoặc I252)!", rdoI251, 0, rdoI251.Height, 5000);
                return;
            }

            // Kiểm tra dữ liệu hợp lệ
            if (!IsDataValid())
            {
                validationToolTip.ToolTipIcon = ToolTipIcon.Warning;
                validationToolTip.ToolTipTitle = "Lỗi";
                validationToolTip.Show("Vui lòng nhập sAPN và ít nhất một trong số các ô tọa độ!", TxtSAPN, 0, TxtSAPN.Height, 5000);
                return;
            }

            // Lấy múi giờ GMT+7
            TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamZone);

            // Xác định ngày và ca làm việc
            string dateString;
            string shift;

            // Nếu thời gian từ 20:00 hôm nay đến trước 08:00 hôm sau, sử dụng ngày bắt đầu ca đêm
            if (vietnamTime.Hour >= 20 || vietnamTime.Hour < 8)
            {
                // Nếu thời gian từ 00:00 đến 07:59:59, sử dụng ngày hôm trước
                if (vietnamTime.Hour < 8)
                {
                    dateString = vietnamTime.AddDays(-1).ToString("yyyyMMdd");
                }
                else
                {
                    dateString = vietnamTime.ToString("yyyyMMdd");
                }
                shift = "NIGHT";
            }
            // Nếu thời gian từ 08:00 đến trước 20:00, sử dụng ngày hiện tại và ca ngày
            else
            {
                dateString = vietnamTime.ToString("yyyyMMdd");
                shift = "DAY";
            }

            string eqpid = ReadEQPIDFromIniFile(); // Lấy EQPID từ file ini
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string appFolderPath = Path.Combine(desktopPath, "IT_CONFIRM_EDITION");
            string fileName = string.IsNullOrEmpty(eqpid) ? $"IT_{dateString}_{shift}.csv" : $"IT_{eqpid}_{dateString}_{shift}.csv";
            string filePath = Path.Combine(appFolderPath, fileName);
            string timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            try
            {
                if (!Directory.Exists(appFolderPath))
                {
                    Directory.CreateDirectory(appFolderPath);
                }

                bool fileExists = File.Exists(filePath);
                if (!fileExists)
                {
                    string header = "MODEL,sAPN,DESCRIPTION,Sx1,Sy1,Ex1,Ey1,Sx2,Sy2,Ex2,Ey2,Sx3,Sy3,Ex3,Ey3,X1,Y1,X2,Y2,X3,Y3,EVENT_TIME";
                    File.AppendAllText(filePath, header + Environment.NewLine, System.Text.Encoding.UTF8);
                }

                // Hàm chuyển đổi từ mm sang pixel hoặc xử lý giá trị "All"
                string ConvertToPixel(string value, string fieldName)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return value; // Giữ nguyên nếu rỗng

                    if (value.Equals("All", StringComparison.OrdinalIgnoreCase) && fieldName == "Sx1")
                    {
                        // Chỉ xử lý "All" cho TxtSx1
                        switch (fieldName)
                        {
                            case "Sx1":
                                return "0";
                            case "Sy1":
                                return "0";
                            case "Ex1":
                                return rdoI251.Checked ? "2420" : "2732";
                            case "Ey1":
                                return rdoI251.Checked ? "1668" : "2048";
                            default:
                                return value; // Không áp dụng cho các trường khác
                        }
                    }

                    if (double.TryParse(value, out double mmValue))
                    {
                        double pixelValue = mmValue * MmToPixelRatio;
                        return ((int)pixelValue).ToString(); // Làm tròn thành số nguyên
                    }
                    return value; // Giữ nguyên nếu không phải số
                }

                // Xử lý nhóm tọa độ đầu tiên (Sx1, Ex1, Sy1, Ey1)
                string GetGroup1Value()
                {
                    if (TxtSx1.Text.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        // Gán giá trị cố định cho cả nhóm, bất kể giá trị của TxtEx1, TxtSy1, TxtEy1
                        return rdoI251.Checked ? "0,0,2420,1668" : "0,0,2732,2048";
                    }
                    return $"{ConvertToPixel(TxtSx1.Text, "Sx1")},{ConvertToPixel(TxtSy1.Text, "Sy1")},{ConvertToPixel(TxtEx1.Text, "Ex1")},{ConvertToPixel(TxtEy1.Text, "Ey1")}";
                }

                // Xử lý các nhóm tọa độ khác (không áp dụng logic "All")
                string GetGroupValue(string sx, string sy, string ex, string ey, string sxField, string syField, string exField, string eyField)
                {
                    return $"{ConvertToPixel(sx, sxField)},{ConvertToPixel(sy, syField)},{ConvertToPixel(ex, exField)},{ConvertToPixel(ey, eyField)}";
                }

                // Lấy model đã chọn
                string selectedModel = rdoI251.Checked ? "I251" : "I252";
                // Lấy loại lỗi đã chọn từ ComboBox
                string selectedErrorType = cboErrorType.SelectedItem.ToString();

                // Áp dụng chuyển đổi cho các nhóm tọa độ
                string csvData = $"{selectedModel},{TxtSAPN.Text},{selectedErrorType}," +
                                 $"{GetGroup1Value()}," +
                                 $"{GetGroupValue(TxtSx2.Text, TxtSy2.Text, TxtEx2.Text, TxtEy2.Text, "Sx2", "Sy2", "Ex2", "Ey2")}," +
                                 $"{GetGroupValue(TxtSx3.Text, TxtSy3.Text, TxtEx3.Text, TxtEy3.Text, "Sx3", "Sy3", "Ex3", "Ey3")}," +
                                 $"{ConvertToPixel(TxtX1.Text, "X1")},{ConvertToPixel(TxtY1.Text, "Y1")}," +
                                 $"{ConvertToPixel(TxtX2.Text, "X2")},{ConvertToPixel(TxtY2.Text, "Y2")}," +
                                 $"{ConvertToPixel(TxtX3.Text, "X3")},{ConvertToPixel(TxtY3.Text, "Y3")},{timestamp}";

                File.AppendAllText(filePath, csvData + Environment.NewLine, System.Text.Encoding.UTF8);
                _lastSavedFilePath = filePath;
                LblStatus.ForeColor = System.Drawing.Color.Green;
                LblStatus.Text = $"Lưu thành công! \nDữ liệu đã được ghi lại lúc: {timestamp}\nVị trí lưu: {filePath}";
                statusToolTip.SetToolTip(LblStatus, "BẤM VÀO ĐÂY ĐỂ MỞ VỊ TRÍ LƯU FILE");

                // Xóa nội dung của tất cả các TextBox sau khi lưu thành công
                TxtSAPN.Clear();
                TxtSx1.Clear();
                TxtSy1.Clear();
                TxtEx1.Clear();
                TxtEy1.Clear();
                TxtSx2.Clear();
                TxtSy2.Clear();
                TxtEx2.Clear();
                TxtEy2.Clear();
                TxtSx3.Clear();
                TxtSy3.Clear();
                TxtEx3.Clear();
                TxtEy3.Clear();
                TxtX1.Clear();
                TxtY1.Clear();
                TxtX2.Clear();
                TxtY2.Clear();
                TxtX3.Clear();
                TxtY3.Clear();
                //cboErrorType.SelectedIndex = 0; // Mặc định chọn "ĐỐM" =-1 KHÔNG CHỌN GÌ

                // Đặt focus lại cho ô đầu tiên
                TxtSAPN.Focus();
                // Cập nhật bộ đếm sau khi lưu
                UpdateSavedSAPNCount();
                // Chạy lưu NAS async (background) để không block UI
                _ = Task.Run(() =>
                {
                    bool nasSaved = true;
                    string nasError = null;
                    try
                    {
                        NetworkCredential nasCredentials = ReadNASCredentialsFromIniFile();
                        using (var connection = new NetworkConnection(nasDirectoryPath, nasCredentials))
                        {
                            // Đảm bảo thư mục NAS tồn tại
                            if (!Directory.Exists(nasDirectoryPath))
                            {
                                Directory.CreateDirectory(nasDirectoryPath);
                            }

                            if (!File.Exists(nasFilePath))
                            {
                                string header = "MODEL,sAPN,DESCRIPTION,Sx1,Sy1,Ex1,Ey1,Sx2,Sy2,Ex2,Ey2,Sx3,Sy3,Ex3,Ey3,X1,Y1,X2,Y2,X3,Y3,EVENT_TIME";
                                File.AppendAllText(nasFilePath, header + Environment.NewLine, System.Text.Encoding.UTF8);
                            }
                            File.AppendAllText(nasFilePath, csvData + Environment.NewLine, System.Text.Encoding.UTF8);
                        }
                    }
                    catch (Win32Exception winEx)
                    {
                        nasSaved = false;
                        nasError = $"Lỗi {winEx.NativeErrorCode}: {winEx.Message}";
                    }
                    catch (Exception ex)
                    {
                        nasSaved = false;
                        nasError = ex.Message;
                    }

                    // Update UI sau khi NAS hoàn thành (dùng Invoke để an toàn)
                    this.Invoke(new Action(() =>
                    {
                        if (nasSaved)
                        {
                            LblStatus.Text += $"\nNAS Server: {nasFilePath}";
                            statusToolTip.SetToolTip(LblStatus, "BẤM VÀO ĐÂY ĐỂ MỞ VỊ TRÍ LƯU FILE");
                        }
                        else
                        {
                            LblStatus.Text += $"\nLưu vào server thất bại: {nasError}";
                            statusToolTip.SetToolTip(LblStatus, "BẤM VÀO ĐÂY ĐỂ MỞ VỊ TRÍ LƯU FILE");
                        }
                    }));
                });
            }
            catch (IOException)
            {
                // Cập nhật thông báo lỗi cụ thể khi file đang được mở
                LblStatus.ForeColor = System.Drawing.Color.Red;
                LblStatus.Text = "File đang được mở bởi ứng dụng khác hoặc không thể ghi dữ liệu.\nHãy đóng file đang mở trước khi bấm Save";
                // Xóa tooltip khi có lỗi
                statusToolTip.SetToolTip(LblStatus, "");
            }
            catch (Exception ex)
            {
                // Báo lỗi chung nếu có lỗi khác
                LblStatus.ForeColor = System.Drawing.Color.Red;
                LblStatus.Text = $"Đã xảy ra lỗi: {ex.Message}";
                // Xóa tooltip khi có lỗi
                statusToolTip.SetToolTip(LblStatus, "");
            }
        }

        // Xử lý nút RESET
        private void BtnReset_Click(object sender, EventArgs e)
        {
            // Xóa nội dung của tất cả các TextBox
            TxtSAPN.Clear();
            TxtSx1.Clear();
            TxtSy1.Clear();
            TxtEx1.Clear();
            TxtEy1.Clear();
            TxtSx2.Clear();
            TxtSy2.Clear();
            TxtEx2.Clear();
            TxtEy2.Clear();
            TxtSx3.Clear();
            TxtSy3.Clear();
            TxtEx3.Clear();
            TxtEy3.Clear();
            TxtX1.Clear();
            TxtY1.Clear();
            TxtX2.Clear();
            TxtY2.Clear();
            TxtX3.Clear();
            TxtY3.Clear();
            cboErrorType.SelectedIndex = -1; // Mặc định chọn "ĐỐM" =0 CHỌN DÒNG ĐẦU TIÊN

            // Cập nhật thông báo
            LblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            LblStatus.Text = "Đã khởi tạo lại ứng dụng.";
            // Xóa tooltip khi reset ứng dụng
            statusToolTip.SetToolTip(LblStatus, "");
            // Đặt focus lại cho ô đầu tiên
            TxtSAPN.Focus();
        }
        private void CoordinateTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox currentTextBox = (TextBox)sender;

            // Kiểm tra và ngăn không cho nhập nếu độ dài đã đạt 3
            if (!char.IsControl(e.KeyChar) && currentTextBox.Text.Length >= 3)
            {
                e.Handled = true;
                validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                validationToolTip.Show("Chỉ cho phép nhập tối đa 3 số", currentTextBox, 0, currentTextBox.Height, 3000);
                return;
            }

            // Cho phép nhập số và các ký tự điều khiển (như Backspace)
            // Ngăn các ký tự khác (chữ cái, ký tự đặc biệt)
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
                validationToolTip.ToolTipTitle = "Lỗi nhập liệu";
                validationToolTip.Show("Chỉ cho phép nhập số!", currentTextBox, 0, currentTextBox.Height, 3000);
            }
        }

        // Phương thức mới để đếm và cập nhật số lượng sAPN đã lưu
        private void UpdateSavedSAPNCount()
        {
            // Lấy múi giờ GMT+7
            TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamZone);

            // Xác định ngày và ca làm việc
            string dateString;
            string shift;

            // Nếu thời gian từ 20:00 hôm nay đến trước 08:00 hôm sau, sử dụng ngày bắt đầu ca đêm
            if (vietnamTime.Hour >= 20 || vietnamTime.Hour < 8)
            {
                // Nếu thời gian từ 00:00 đến 07:59:59, sử dụng ngày hôm trước
                if (vietnamTime.Hour < 8)
                {
                    dateString = vietnamTime.AddDays(-1).ToString("yyyyMMdd");
                }
                else
                {
                    dateString = vietnamTime.ToString("yyyyMMdd");
                }
                shift = "NIGHT";
            }
            // Nếu thời gian từ 08:00 đến trước 20:00, sử dụng ngày hiện tại và ca ngày
            else
            {
                dateString = vietnamTime.ToString("yyyyMMdd");
                shift = "DAY";
            }

            string eqpid = ReadEQPIDFromIniFile(); // Lấy EQPID từ file ini
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string appFolderPath = Path.Combine(desktopPath, "IT_CONFIRM_EDITION");
            string fileName = string.IsNullOrEmpty(eqpid) ? $"IT_{dateString}_{shift}.csv" : $"IT_{eqpid}_{dateString}_{shift}.csv";
            string filePath = Path.Combine(appFolderPath, fileName);

            int count = 0;
            if (File.Exists(filePath))
            {
                try
                {
                    var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                    // Bỏ qua dòng tiêu đề
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) // Kiểm tra cột sAPN
                        {
                            // Kiểm tra xem có ít nhất một tọa độ không rỗng
                            bool hasCoordinates = false;
                            for (int j = 3; j < parts.Length - 1; j++) // Bắt đầu từ cột sau ERROR_TYPE
                            {
                                if (!string.IsNullOrWhiteSpace(parts[j]))
                                {
                                    hasCoordinates = true;
                                    break;
                                }
                            }
                            if (hasCoordinates)
                            {
                                count++;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // Bỏ qua lỗi nếu file đang được mở, không cập nhật bộ đếm
                    return;
                }
                catch (Exception ex)
                {
                    // Xử lý các lỗi khác nếu có
                    LblStatus.ForeColor = System.Drawing.Color.Red;
                    LblStatus.Text = $"Lỗi khi đọc file đếm số lượng: {ex.Message}";
                    return;
                }
            }
            LblSAPNCount.Text = $"Số lượng APN đã lưu: {count}";
        }
        #endregion

        #region TẠO ĐƯỜNG DẪN FILE CỤC BỘ VÀ NAS
        // Phương thức tạo đường dẫn file cục bộ và NAS
        private void SetFilePath()
        {
            // Lấy múi giờ GMT+7
            TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamZone);

            // Xác định ngày và ca làm việc
            string dateString;
            string shift;

            // Nếu thời gian từ 20:00 hôm nay đến trước 08:00 hôm sau, sử dụng ngày bắt đầu ca đêm
            if (vietnamTime.Hour >= 20 || vietnamTime.Hour < 8)
            {
                // Nếu thời gian từ 00:00 đến 07:59:59, sử dụng ngày hôm trước
                if (vietnamTime.Hour < 8)
                {
                    dateString = vietnamTime.AddDays(-1).ToString("yyyyMMdd");
                }
                else
                {
                    dateString = vietnamTime.ToString("yyyyMMdd");
                }
                shift = "NIGHT";
            }
            // Nếu thời gian từ 08:00 đến trước 20:00, sử dụng ngày hiện tại và ca ngày
            else
            {
                dateString = vietnamTime.ToString("yyyyMMdd");
                shift = "DAY";
            }

            string eqpid = ReadEQPIDFromIniFile(); // Lấy EQPID từ file ini
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string directoryPath = Path.Combine(desktopPath, "IT_CONFIRM");
            // Tạo tên file với EQPID (nếu có)
            string fileName = string.IsNullOrEmpty(eqpid) ? $"IT_{dateString}_{shift}.csv" : $"IT_{eqpid}_{dateString}_{shift}.csv";
            _lastSavedFilePath = Path.Combine(directoryPath, fileName);

            // Tạo đường dẫn NAS tương tự
            nasFilePath = Path.Combine(nasDirectoryPath, fileName); // ĐÚNG

            // Đảm bảo thư mục tồn tại
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {
                LblStatus.ForeColor = System.Drawing.Color.Red;
                LblStatus.Text = $"Lỗi tạo thư mục: {ex.Message}";
            }
        }
        #endregion

        #region ĐỌC THÔNG TIN TỪ FILE INI
        /// <summary>
        /// Đọc giá trị EQPID từ file MachineParam.ini
        /// </summary>
        /// <returns>Giá trị EQPID hoặc chuỗi rỗng nếu không tìm thấy.</returns>
        private string ReadEQPIDFromIniFile()
        {
            string filePath = @"C:\samsung\Debug\Config\MachineParam.ini";
            if (File.Exists(filePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("EQPID="))
                        {
                            return line.Substring("EQPID=".Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi khi đọc file, ví dụ: không có quyền truy cập
                    MessageBox.Show("Lỗi khi đọc file MachineParam.ini: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return "";
        }

        /// <summary>
        /// Đọc giá trị NASPATH, NASUSER, NASPASSWORD, NASDOMAIN từ section [NAS SERVER] trong file NAS.ini, tạo file nếu chưa tồn tại
        /// </summary>
        /// <returns>NetworkCredential chứa thông tin xác thực NAS</returns>
        private NetworkCredential ReadNASCredentialsFromIniFile()
        {
            string filePath = @"C:\IT_CONFIRM\Config\NAS_EDITION.ini";
            string defaultNasPath = @"\\107.126.41.111\IT_CONFIRM";
            string defaultNasUser = "admin";
            string defaultNasPassword = "insp2019@";
            string defaultNasDomain = "";

            try
            {
                // Đảm bảo thư mục tồn tại
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Kiểm tra quyền ghi vào thư mục
                if (!IsDirectoryWritable(directoryPath))
                {
                    LblStatus.ForeColor = System.Drawing.Color.Red;
                    LblStatus.Text = $"Không có quyền ghi vào thư mục {directoryPath} để tạo NAS.ini";
                    return new NetworkCredential(defaultNasUser, defaultNasPassword, defaultNasDomain);
                }

                // Nếu file chưa tồn tại, tạo file với định dạng INI có section [NAS SERVER]
                if (!File.Exists(filePath))
                {
                    string iniContent = "[NAS SERVER]\n" +
                                       $"NASPATH={defaultNasPath}\n" +
                                       $"NASUSER={defaultNasUser}\n" +
                                       $"NASPASSWORD={defaultNasPassword}\n" +
                                       $"NASDOMAIN={defaultNasDomain}";
                    File.WriteAllText(filePath, iniContent, Encoding.UTF8);
                    //LblStatus.ForeColor = System.Drawing.Color.Blue;
                    //LblStatus.Text = $"Đã tạo file NAS.ini tại {filePath}";
                    nasDirectoryPath = defaultNasPath; // Gán mặc định
                    return new NetworkCredential(defaultNasUser, defaultNasPassword, defaultNasDomain);
                }

                // Đọc file
                string[] lines = File.ReadAllLines(filePath);
                string nasPath = defaultNasPath;
                string nasUser = defaultNasUser;
                string nasPassword = defaultNasPassword;
                string nasDomain = defaultNasDomain;
                bool inNasServerSection = false;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    // Kiểm tra section [NAS SERVER]
                    if (trimmedLine.Equals("[NAS SERVER]"))
                    {
                        inNasServerSection = true;
                        continue;
                    }

                    // Chỉ đọc các khóa trong section [NAS SERVER]
                    if (inNasServerSection)
                    {
                        if (trimmedLine.StartsWith("NASPATH="))
                            nasPath = trimmedLine.Substring("NASPATH=".Length);
                        else if (trimmedLine.StartsWith("NASUSER="))
                            nasUser = trimmedLine.Substring("NASUSER=".Length);
                        else if (trimmedLine.StartsWith("NASPASSWORD="))
                            nasPassword = trimmedLine.Substring("NASPASSWORD=".Length);
                        else if (trimmedLine.StartsWith("NASDOMAIN="))
                            nasDomain = trimmedLine.Substring("NASDOMAIN=".Length);
                    }
                }

                // Gán nasDirectoryPath (chỉ trong constructor, không gây lỗi readonly)
                nasDirectoryPath = nasPath;
                return new NetworkCredential(nasUser, nasPassword, nasDomain);
            }
            catch (Exception ex)
            {
                LblStatus.ForeColor = System.Drawing.Color.Red;
                LblStatus.Text = $"Lỗi khi đọc/tạo file NAS.ini: {ex.Message}";
                nasDirectoryPath = defaultNasPath; // Gán mặc định để tránh null
                return new NetworkCredential(defaultNasUser, defaultNasPassword, defaultNasDomain);
            }
        }

        // Phương thức kiểm tra quyền ghi thư mục
        private bool IsDirectoryWritable(string directoryPath)
        {
            try
            {
                using (FileStream fs = File.Create(Path.Combine(directoryPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                { }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region TIP HƯỚNG DẪN
        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Tạo form hướng dẫn mới
            MAP mapForm = new MAP();

            // Hiển thị form dưới dạng Dialog
            mapForm.ShowDialog();
        }
        #endregion
    }
}