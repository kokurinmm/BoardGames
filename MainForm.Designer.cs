namespace BoardGames
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            groupBox1 = new GroupBox();
            rbCorners = new RadioButton();
            rbReversi = new RadioButton();
            rbCheckers = new RadioButton();
            groupBox2 = new GroupBox();
            nudMctsMs = new NumericUpDown();
            label3 = new Label();
            rbMcts = new RadioButton();
            nudDepth = new NumericUpDown();
            label1 = new Label();
            rbAlphaBeta = new RadioButton();
            lblStatus = new Label();
            pnlBoard = new Panel();
            btnNewGame = new Button();
            btnNoAiGame = new Button();
            btnHelp = new Button();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudMctsMs).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDepth).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(rbCorners);
            groupBox1.Controls.Add(rbReversi);
            groupBox1.Controls.Add(rbCheckers);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(261, 117);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Игра";
            // 
            // rbCorners
            // 
            rbCorners.AutoSize = true;
            rbCorners.Location = new Point(135, 73);
            rbCorners.Name = "rbCorners";
            rbCorners.Size = new Size(93, 29);
            rbCorners.TabIndex = 2;
            rbCorners.TabStop = true;
            rbCorners.Text = "Уголки";
            rbCorners.UseVisualStyleBackColor = true;
            // 
            // rbReversi
            // 
            rbReversi.AutoSize = true;
            rbReversi.Location = new Point(12, 73);
            rbReversi.Name = "rbReversi";
            rbReversi.Size = new Size(104, 29);
            rbReversi.TabIndex = 1;
            rbReversi.TabStop = true;
            rbReversi.Text = "Реверси";
            rbReversi.UseVisualStyleBackColor = true;
            // 
            // rbCheckers
            // 
            rbCheckers.AutoSize = true;
            rbCheckers.Checked = true;
            rbCheckers.Location = new Point(12, 38);
            rbCheckers.Name = "rbCheckers";
            rbCheckers.Size = new Size(96, 29);
            rbCheckers.TabIndex = 0;
            rbCheckers.TabStop = true;
            rbCheckers.Text = "Шашки";
            rbCheckers.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(nudMctsMs);
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(rbMcts);
            groupBox2.Controls.Add(nudDepth);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(rbAlphaBeta);
            groupBox2.Location = new Point(12, 147);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(261, 199);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Алгоритм ИИ";
            // 
            // nudMctsMs
            // 
            nudMctsMs.Increment = new decimal(new int[] { 250, 0, 0, 0 });
            nudMctsMs.Location = new Point(150, 155);
            nudMctsMs.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            nudMctsMs.Minimum = new decimal(new int[] { 250, 0, 0, 0 });
            nudMctsMs.Name = "nudMctsMs";
            nudMctsMs.ReadOnly = true;
            nudMctsMs.Size = new Size(74, 31);
            nudMctsMs.TabIndex = 8;
            nudMctsMs.Value = new decimal(new int[] { 750, 0, 0, 0 });
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(10, 155);
            label3.Name = "label3";
            label3.Size = new Size(125, 25);
            label3.TabIndex = 7;
            label3.Text = "Миллисекунд:";
            // 
            // rbMcts
            // 
            rbMcts.AutoSize = true;
            rbMcts.Location = new Point(12, 120);
            rbMcts.Name = "rbMcts";
            rbMcts.Size = new Size(228, 29);
            rbMcts.TabIndex = 6;
            rbMcts.TabStop = true;
            rbMcts.Text = "Monte Carlo Tree Search";
            rbMcts.UseVisualStyleBackColor = true;
            // 
            // nudDepth
            // 
            nudDepth.Location = new Point(120, 70);
            nudDepth.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            nudDepth.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            nudDepth.Name = "nudDepth";
            nudDepth.ReadOnly = true;
            nudDepth.Size = new Size(74, 31);
            nudDepth.TabIndex = 3;
            nudDepth.Value = new decimal(new int[] { 4, 0, 0, 0 });
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(10, 70);
            label1.Name = "label1";
            label1.Size = new Size(81, 25);
            label1.TabIndex = 2;
            label1.Text = "Глубина:";
            // 
            // rbAlphaBeta
            // 
            rbAlphaBeta.AutoSize = true;
            rbAlphaBeta.Checked = true;
            rbAlphaBeta.Location = new Point(12, 35);
            rbAlphaBeta.Name = "rbAlphaBeta";
            rbAlphaBeta.Size = new Size(125, 29);
            rbAlphaBeta.TabIndex = 0;
            rbAlphaBeta.TabStop = true;
            rbAlphaBeta.Text = "Alpha-beta";
            rbAlphaBeta.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblStatus.Location = new Point(14, 369);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(259, 39);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Вы:";
            // 
            // pnlBoard
            // 
            pnlBoard.Location = new Point(300, 0);
            pnlBoard.Name = "pnlBoard";
            pnlBoard.Size = new Size(520, 528);
            pnlBoard.TabIndex = 3;
            // 
            // btnNewGame
            // 
            btnNewGame.Location = new Point(12, 426);
            btnNewGame.Name = "btnNewGame";
            btnNewGame.Size = new Size(261, 34);
            btnNewGame.TabIndex = 4;
            btnNewGame.Text = "Новая игра";
            btnNewGame.UseVisualStyleBackColor = true;
            // 
            // btnNoAiGame
            // 
            btnNoAiGame.Location = new Point(12, 479);
            btnNoAiGame.Name = "btnNoAiGame";
            btnNoAiGame.Size = new Size(182, 34);
            btnNoAiGame.TabIndex = 5;
            btnNoAiGame.Text = "Играть без ИИ";
            btnNoAiGame.UseVisualStyleBackColor = true;
            // 
            // btnHelp
            // 
            btnHelp.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnHelp.Location = new Point(212, 479);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(61, 34);
            btnHelp.TabIndex = 6;
            btnHelp.Text = "?";
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(821, 529);
            Controls.Add(btnHelp);
            Controls.Add(btnNoAiGame);
            Controls.Add(btnNewGame);
            Controls.Add(pnlBoard);
            Controls.Add(lblStatus);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "Шашки и Реверси";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudMctsMs).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDepth).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private RadioButton rbReversi;
        private RadioButton rbCheckers;
        private GroupBox groupBox2;
        private RadioButton rbAlphaBeta;
        private Label label1;
        private NumericUpDown nudDepth;
        private Label lblStatus;
        private Panel pnlBoard;
        private Button btnNewGame;
        private Button btnNoAiGame;
        private RadioButton rbCorners;
        private RadioButton rbMcts;
        private NumericUpDown nudMctsMs;
        private Label label3;
        private Button btnHelp;
    }
}
