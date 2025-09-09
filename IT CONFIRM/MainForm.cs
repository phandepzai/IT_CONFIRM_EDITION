using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace IT_CONFIRM
{
    public partial class MainForm : Form
    {


        #region KHAI BÁO CÁC BIẾN
        private TextBox currentTextBox;
        private ToolTip validationToolTip;
        private string _lastSavedFilePath; // Biến mới để lưu đường dẫn file        
        private ToolTip statusToolTip;// Biến mới cho ToolTip của thông báo trạng thái     
        private Timer rainbowTimer;// Các biến cho hiệu ứng chuyển màu cầu vồng mượt mà
        private bool isRainbowActive = false;
        private Color originalCopyrightColor;
        private double rainbowPhase = 0;       
        private Dictionary<string, Color> originalColors = new Dictionary<string, Color>();// Sử dụng Dictionary để lưu màu gốc của tất cả các nút
        private RadioButton rdoI251; // Khai báo RadioButton cho I251
        private RadioButton rdoI252; // Khai báo RadioButton cho I252

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
            InitializeKeyboardEvents();
            txtSAPN.MaxLength = 300;
            validationToolTip = new ToolTip();//Thông báo yêu cầu nhập dữ liệu
            statusToolTip = new ToolTip();//Gợi ý bấm vào để mở thư mục
            this.lblStatus.Text = "Sẵn sàng nhập dữ liệu...";
            UpdateSavedSAPNCount();

            // Khởi tạo ComboBox với danh sách lỗi
            cboErrorType.Items.AddRange(new string[] { "B-SPOT", "W-POT", "ĐỐM SPIN", "ĐỐM PANEL", "ĐỐM ĐƯỜNG DỌC", "-" });
            cboErrorType.SelectedIndex = -1; // Mặc định chọn "ĐỐM" =-1 KHÔNG CHỌN GÌ

            // Khởi tạo timer cho hiệu ứng cầu vồng
            this.rainbowTimer = new Timer();
            this.rainbowTimer.Interval = 20; // Cập nhật màu mỗi 20ms để mượt hơn
            this.rainbowTimer.Tick += new EventHandler(this.RainbowTimer_Tick);

            // Gắn sự kiện cho lblCopyright
            lblCopyright.MouseEnter += LblCopyright_MouseEnter;
            lblCopyright.MouseLeave += LblCopyright_MouseLeave;
            // Gán sự kiện Click và thay đổi con trỏ chuột cho lblStatus
            this.lblStatus.Click += new EventHandler(this.lblStatus_Click);
            this.lblStatus.Cursor = Cursors.Hand;

            // Khởi tạo hiệu ứng cho các nút
            InitializeButtonEffects();
        }
        #endregion

        #region HIỆU ỨNG CHO CÁC NÚT BẤM
        private void InitializeButtonEffects()
        {
            Color keyboardBaseColor = System.Drawing.ColorTranslator.FromHtml("#F5F5DC");

            // Xử lý nút SAVE (giữ nguyên màu ban đầu)
            originalColors.Add("btnSave", btnSave.BackColor);
            btnSave.MouseEnter += Button_MouseEnter;
            btnSave.MouseLeave += Button_MouseLeave;
            btnSave.MouseDown += Button_MouseDown;
            btnSave.MouseUp += Button_MouseUp;

            // Xử lý nút RESET (giữ nguyên màu ban đầu)
            originalColors.Add("btnReset", btnReset.BackColor);
            btnReset.MouseEnter += Button_MouseEnter;
            btnReset.MouseLeave += Button_MouseLeave;
            btnReset.MouseDown += Button_MouseDown;
            btnReset.MouseUp += Button_MouseUp;

            // Đặt lại màu cho nút ALL và xử lý riêng biệt
            btnAll.BackColor = System.Drawing.ColorTranslator.FromHtml("#97FFFF");
            originalColors.Add("btnAll", btnAll.BackColor);
            btnAll.MouseEnter += Button_MouseEnter;
            btnAll.MouseLeave += Button_MouseLeave;
            btnAll.MouseDown += Button_MouseDown;
            btnAll.MouseUp += Button_MouseUp;

            // Áp dụng màu nền mới cho các nút trên bàn phím ảo (trừ nút ALL và btnBack)
            Button[] keyboardButtons = { btn0, btn1, btn2, btn3, btn4, btn5, btn6, btn7, btn8, btn9, btnBack };
            foreach (Button btn in keyboardButtons)
            {
                // Chỉ áp dụng màu nền mới cho các nút số
                if (btn.Name != "btnBack")
                {
                    btn.BackColor = keyboardBaseColor;
                }

                // Lưu màu nền hiện tại (đã được đổi) vào từ điển
                originalColors.Add(btn.Name, btn.BackColor);

                btn.MouseEnter += Button_MouseEnter;
                btn.MouseLeave += Button_MouseLeave;
                btn.MouseDown += Button_MouseDown;
                btn.MouseUp += Button_MouseUp;
            }
        }

        // Hiệu ứng khi di chuột vào nút
        private void Button_MouseEnter(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                // Áp dụng hiệu ứng di chuột cho TẤT CẢ các nút
                if (originalColors.ContainsKey(btn.Name))
                {
                    Color originalColor = originalColors[btn.Name];
                    int r = Math.Min(255, originalColor.R + 30);
                    int g = Math.Min(255, originalColor.G + 30);
                    int b = Math.Min(255, originalColor.B + 30);
                    btn.BackColor = Color.FromArgb(r, g, b);
                }
            }
        }

        private void Button_MouseLeave(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && originalColors.ContainsKey(btn.Name))
            {
                // Khôi phục màu nền ban đầu
                btn.BackColor = originalColors[btn.Name];
            }
        }

        private void Button_MouseDown(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                // Áp dụng hiệu ứng khi nhấn chuột xuống cho TẤT CẢ các nút
                if (originalColors.ContainsKey(btn.Name))
                {
                    Color originalColor = originalColors[btn.Name];
                    int r = Math.Max(0, originalColor.R - 30);
                    int g = Math.Max(0, originalColor.G - 30);
                    int b = Math.Max(0, originalColor.B - 30);
                    btn.BackColor = Color.FromArgb(r, g, b);
                }
            }
        }

        private void Button_MouseUp(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                if (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
                {
                    // Nếu nhả chuột trong vùng nút, khôi phục hiệu ứng di chuột vào
                    if (originalColors.ContainsKey(btn.Name))
                    {
                        Color originalColor = originalColors[btn.Name];
                        int r = Math.Min(255, originalColor.R + 30);
                        int g = Math.Min(255, originalColor.G + 30);
                        int b = Math.Min(255, originalColor.B + 30);
                        btn.BackColor = Color.FromArgb(r, g, b);
                    }
                }
                else
                {
                    // Nếu nhả chuột ngoài vùng nút, khôi phục màu ban đầu
                    if (originalColors.ContainsKey(btn.Name))
                    {
                        btn.BackColor = originalColors[btn.Name];
                    }
                }
            }
        }
        #endregion

        #region XỬ LÝ BÀN PHÍM ẢO VÀ KIỂM TRA DỮ LIỆU
        // Hàm này được gọi trong constructor
        private void InitializeKeyboardEvents()
        {
            // Gán sự kiện cho các TextBox để biết TextBox nào đang được chọn
            txtSAPN.Click += TextBox_Click;
            txtSx1.Click += TextBox_Click;
            txtSy1.Click += TextBox_Click;
            txtEx1.Click += TextBox_Click;
            txtEy1.Click += TextBox_Click;
            txtSx2.Click += TextBox_Click;
            txtSy2.Click += TextBox_Click;
            txtEx2.Click += TextBox_Click;
            txtEy2.Click += TextBox_Click;
            txtSx3.Click += TextBox_Click;
            txtSy3.Click += TextBox_Click;
            txtEx3.Click += TextBox_Click;
            txtEy3.Click += TextBox_Click;
            txtX1.Click += TextBox_Click;
            txtY1.Click += TextBox_Click;
            txtX2.Click += TextBox_Click;
            txtY2.Click += TextBox_Click;
            txtX3.Click += TextBox_Click;
            txtY3.Click += TextBox_Click;

            // Gán sự kiện kiểm tra phím bấm cho các ô tọa độ
            txtSAPN.KeyDown += txtSAPN_KeyDown;
            txtSx1.KeyPress += CoordinateTextBox_KeyPress;
            txtSy1.KeyPress += CoordinateTextBox_KeyPress;
            txtEx1.KeyPress += CoordinateTextBox_KeyPress;
            txtEy1.KeyPress += CoordinateTextBox_KeyPress;
            txtSx2.KeyPress += CoordinateTextBox_KeyPress;
            txtSy2.KeyPress += CoordinateTextBox_KeyPress;
            txtEx2.KeyPress += CoordinateTextBox_KeyPress;
            txtEy2.KeyPress += CoordinateTextBox_KeyPress;
            txtSx3.KeyPress += CoordinateTextBox_KeyPress;
            txtSy3.KeyPress += CoordinateTextBox_KeyPress;
            txtEx3.KeyPress += CoordinateTextBox_KeyPress;
            txtEy3.KeyPress += CoordinateTextBox_KeyPress;
            txtX1.KeyPress += CoordinateTextBox_KeyPress;
            txtY1.KeyPress += CoordinateTextBox_KeyPress;
            txtX2.KeyPress += CoordinateTextBox_KeyPress;
            txtY2.KeyPress += CoordinateTextBox_KeyPress;
            txtX3.KeyPress += CoordinateTextBox_KeyPress;
            txtY3.KeyPress += CoordinateTextBox_KeyPress;
        }
        // Khi chuột di vào nhãn, kích hoạt hiệu ứng cầu vồng
        private void LblCopyright_MouseEnter(object sender, EventArgs e)
        {
            if (!isRainbowActive)
            {
                isRainbowActive = true;
                originalCopyrightColor = lblCopyright.ForeColor;
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
                lblCopyright.ForeColor = originalCopyrightColor;
            }
        }
        #endregion

        #region THAY ĐỔI MÀU SẮC KHI DI CHUỘT VÀO TÊN TÁC GIẢ
        // Sự kiện Tick của timer, cập nhật màu sắc
        private void RainbowTimer_Tick(object sender, EventArgs e)
        {
            rainbowPhase += 0.05; // Giảm tốc độ thay đổi để màu chuyển từ từ hơn

            Color newColor = CalculateRainbowColor(rainbowPhase);
            lblCopyright.ForeColor = newColor;
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

        #region LABEL TRẠNG THÁI VÀ MỞ THƯ MỤC
        // Phương thức xử lý sự kiện Click cho lblStatus
        private void lblStatus_Click(object sender, EventArgs e)
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

        // Phương thức xử lý sự kiện KeyDown của txtSAPN
        private void txtSAPN_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                txtSx1.Focus();
                currentTextBox = txtSx1; // Cập nhật currentTextBox để bàn phím ảo tương tác với txtSx1
                e.SuppressKeyPress = true;
            }
        }

        // Xử lý các nút số trên bàn phím ảo
        private void btnNumber_Click(object sender, EventArgs e)
        {
            if (currentTextBox != null)
            {
                Button btn = (Button)sender;
                string buttonText = btn.Text;

                // Chỉ cho phép nhập "All" vào txtSx1
                if (buttonText.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentTextBox != txtSx1 || !string.IsNullOrWhiteSpace(currentTextBox.Text))
                    {
                        return; // Không làm gì nếu không phải txtSx1 hoặc ô đã có nội dung
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

        // Xử lý nút xóa (Back)
        private void btnBack_Click(object sender, EventArgs e)
        {
            if (currentTextBox != null && currentTextBox.Text.Length > 0)
            {
                currentTextBox.Text = currentTextBox.Text.Substring(0, currentTextBox.Text.Length - 1);
            }
        }

        // Kiểm tra dữ liệu sAPN và ít nhất một trường tọa độ
        private bool IsDataValid()
        {
            if (string.IsNullOrWhiteSpace(txtSAPN.Text))
            {
                return false;
            }

            // Kiểm tra xem ít nhất một trong các ô tọa độ có dữ liệu không
            if (string.IsNullOrWhiteSpace(txtSx1.Text) && string.IsNullOrWhiteSpace(txtSy1.Text) && string.IsNullOrWhiteSpace(txtEx1.Text) && string.IsNullOrWhiteSpace(txtEy1.Text) &&
                string.IsNullOrWhiteSpace(txtSx2.Text) && string.IsNullOrWhiteSpace(txtSy2.Text) && string.IsNullOrWhiteSpace(txtEx2.Text) && string.IsNullOrWhiteSpace(txtEy2.Text) &&
                string.IsNullOrWhiteSpace(txtSx3.Text) && string.IsNullOrWhiteSpace(txtSy3.Text) && string.IsNullOrWhiteSpace(txtEx3.Text) && string.IsNullOrWhiteSpace(txtEy3.Text) &&
                string.IsNullOrWhiteSpace(txtX1.Text) && string.IsNullOrWhiteSpace(txtY1.Text) && string.IsNullOrWhiteSpace(txtX2.Text) && string.IsNullOrWhiteSpace(txtY2.Text) &&
                string.IsNullOrWhiteSpace(txtX3.Text) && string.IsNullOrWhiteSpace(txtY3.Text))
            {
                return false;
            }
            return true;
        }

        #endregion

        #region SƯ KIỆN BẤM NÚT SAVE VÀ RESET
        // Xử lý nút SAVE
        private void btnSave_Click(object sender, EventArgs e)
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
                validationToolTip.Show("Vui lòng nhập sAPN và ít nhất một trong số các ô tọa độ!", txtSAPN, 0, txtSAPN.Height, 5000);
                return;
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string appFolderPath = Path.Combine(desktopPath, "IT_CONFIRM_EDITION");
            string fileName = $"TOA DO_{DateTime.Now:yyyyMMdd}.csv";
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
                        // Chỉ xử lý "All" cho txtSx1
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
                    if (txtSx1.Text.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        // Gán giá trị cố định cho cả nhóm, bất kể giá trị của txtEx1, txtSy1, txtEy1
                        return rdoI251.Checked ? "0,0,2420,1668" : "0,0,2732,2048";
                    }
                    return $"{ConvertToPixel(txtSx1.Text, "Sx1")},{ConvertToPixel(txtSy1.Text, "Sy1")},{ConvertToPixel(txtEx1.Text, "Ex1")},{ConvertToPixel(txtEy1.Text, "Ey1")}";
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
                string csvData = $"{selectedModel},{txtSAPN.Text},{selectedErrorType}," +
                                 $"{GetGroup1Value()}," +
                                 $"{GetGroupValue(txtSx2.Text, txtSy2.Text, txtEx2.Text, txtEy2.Text, "Sx2", "Sy2", "Ex2", "Ey2")}," +
                                 $"{GetGroupValue(txtSx3.Text, txtSy3.Text, txtEx3.Text, txtEy3.Text, "Sx3", "Sy3", "Ex3", "Ey3")}," +
                                 $"{ConvertToPixel(txtX1.Text, "X1")},{ConvertToPixel(txtY1.Text, "Y1")}," +
                                 $"{ConvertToPixel(txtX2.Text, "X2")},{ConvertToPixel(txtY2.Text, "Y2")}," +
                                 $"{ConvertToPixel(txtX3.Text, "X3")},{ConvertToPixel(txtY3.Text, "Y3")},{timestamp}";

                File.AppendAllText(filePath, csvData + Environment.NewLine, System.Text.Encoding.UTF8);
                _lastSavedFilePath = filePath;
                lblStatus.ForeColor = System.Drawing.Color.Green;
                lblStatus.Text = $"Lưu thành công lúc gần đây nhất {timestamp}\nDữ liệu được lưu tại {filePath}";
                statusToolTip.SetToolTip(lblStatus, "Bấm vào đây để mở thư mục lưu file");

                txtSAPN.Clear();
                txtSx1.Clear();
                txtSy1.Clear();
                txtEx1.Clear();
                txtEy1.Clear();
                txtSx2.Clear();
                txtSy2.Clear();
                txtEx2.Clear();
                txtEy2.Clear();
                txtSx3.Clear();
                txtSy3.Clear();
                txtEx3.Clear();
                txtEy3.Clear();
                txtX1.Clear();
                txtY1.Clear();
                txtX2.Clear();
                txtY2.Clear();
                txtX3.Clear();
                txtY3.Clear();

                txtSAPN.Focus();
                UpdateSavedSAPNCount();
            }
            catch (IOException)
            {
                lblStatus.ForeColor = System.Drawing.Color.Red;
                lblStatus.Text = "File đang được mở bởi ứng dụng khác hoặc không thể ghi dữ liệu.\nHãy đóng file đang mở trước khi bấm Save";
                statusToolTip.SetToolTip(lblStatus, "");
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = System.Drawing.Color.Red;
                lblStatus.Text = $"Đã xảy ra lỗi: {ex.Message}";
                statusToolTip.SetToolTip(lblStatus, "");
            }
        }

        // Xử lý nút RESET
        private void btnReset_Click(object sender, EventArgs e)
        {
            // Xóa nội dung của tất cả các TextBox
            txtSAPN.Clear();
            txtSx1.Clear();
            txtSy1.Clear();
            txtEx1.Clear();
            txtEy1.Clear();
            txtSx2.Clear();
            txtSy2.Clear();
            txtEx2.Clear();
            txtEy2.Clear();
            txtSx3.Clear();
            txtSy3.Clear();
            txtEx3.Clear();
            txtEy3.Clear();
            txtX1.Clear();
            txtY1.Clear();
            txtX2.Clear();
            txtY2.Clear();
            txtX3.Clear();
            txtY3.Clear();

            // Cập nhật thông báo
            lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            lblStatus.Text = "Đã khởi tạo lại ứng dụng.";
            // Xóa tooltip khi reset ứng dụng
            statusToolTip.SetToolTip(lblStatus, "");
            // Đặt focus lại cho ô đầu tiên
            txtSAPN.Focus();
        }
        private void CoordinateTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            // Chỉ cho phép nhập "a", "l" vào txtSx1
            if (textBox != txtSx1 && (char.ToLower(e.KeyChar) == 'a' || char.ToLower(e.KeyChar) == 'l'))
            {
                e.Handled = true;
                return;
            }

            // Kiểm tra độ dài tổng của ô nhập liệu
            if (!char.IsControl(e.KeyChar) && textBox.Text.Length >= 4)
            {
                e.Handled = true;
                return;
            }

            // Cho phép nhập số, dấu chấm, dấu phẩy (và "a", "l" cho txtSx1)
            if (!char.IsControl(e.KeyChar) &&
                !char.IsDigit(e.KeyChar) &&
                (e.KeyChar != '.') &&
                (e.KeyChar != ',') &&
                !(textBox == txtSx1 && (char.ToLower(e.KeyChar) == 'a' || char.ToLower(e.KeyChar) == 'l')))
            {
                e.Handled = true;
            }

            // Chỉ cho phép một dấu chấm hoặc một dấu phẩy
            if ((e.KeyChar == '.') && (textBox.Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
            if ((e.KeyChar == ',') && (textBox.Text.IndexOf(',') > -1))
            {
                e.Handled = true;
            }
        }

        // Phương thức mới để đếm và cập nhật số lượng sAPN đã lưu
        private void UpdateSavedSAPNCount()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string appFolderPath = Path.Combine(desktopPath, "IT_CONFIRM_EDITION");
            string fileName = $"TOA DO_{DateTime.Now:yyyyMMdd}.csv";
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
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    lblStatus.Text = $"Lỗi khi đọc file đếm số lượng: {ex.Message}";
                    return;
                }
            }
            lblSAPNCount.Text = $"Số lượng APN đã lưu: {count}";
        }
        #endregion
    }
}