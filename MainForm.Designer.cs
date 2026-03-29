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
            rbReversi = new RadioButton();
            rbCheckers = new RadioButton();
            groupBox2 = new GroupBox();
            nudSims = new NumericUpDown();
            label2 = new Label();
            nudDepth = new NumericUpDown();
            label1 = new Label();
            rbMonteCarlo = new RadioButton();
            rbAlphaBeta = new RadioButton();
            lblStatus = new Label();
            pnlBoard = new Panel();
            btnNewGame = new Button();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudSims).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDepth).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(rbReversi);
            groupBox1.Controls.Add(rbCheckers);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(261, 117);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Игра";
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
            groupBox2.Controls.Add(nudSims);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(nudDepth);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(rbMonteCarlo);
            groupBox2.Controls.Add(rbAlphaBeta);
            groupBox2.Location = new Point(12, 147);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(261, 199);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Алгоритм";
            // 
            // nudSims
            // 
            nudSims.Location = new Point(121, 146);
            nudSims.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            nudSims.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            nudSims.Name = "nudSims";
            nudSims.Size = new Size(74, 31);
            nudSims.TabIndex = 5;
            nudSims.Value = new decimal(new int[] { 50, 0, 0, 0 });
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(10, 146);
            label2.Name = "label2";
            label2.Size = new Size(108, 25);
            label2.TabIndex = 4;
            label2.Text = "Симуляций:";
            // 
            // nudDepth
            // 
            nudDepth.Location = new Point(121, 65);
            nudDepth.Maximum = new decimal(new int[] { 7, 0, 0, 0 });
            nudDepth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudDepth.Name = "nudDepth";
            nudDepth.Size = new Size(74, 31);
            nudDepth.TabIndex = 3;
            nudDepth.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(10, 67);
            label1.Name = "label1";
            label1.Size = new Size(81, 25);
            label1.TabIndex = 2;
            label1.Text = "Глубина:";
            // 
            // rbMonteCarlo
            // 
            rbMonteCarlo.AutoSize = true;
            rbMonteCarlo.Location = new Point(10, 114);
            rbMonteCarlo.Name = "rbMonteCarlo";
            rbMonteCarlo.Size = new Size(145, 29);
            rbMonteCarlo.TabIndex = 1;
            rbMonteCarlo.TabStop = true;
            rbMonteCarlo.Text = "Монте Карло";
            rbMonteCarlo.UseVisualStyleBackColor = true;
            // 
            // rbAlphaBeta
            // 
            rbAlphaBeta.AutoSize = true;
            rbAlphaBeta.Checked = true;
            rbAlphaBeta.Location = new Point(10, 35);
            rbAlphaBeta.Name = "rbAlphaBeta";
            rbAlphaBeta.Size = new Size(130, 29);
            rbAlphaBeta.TabIndex = 0;
            rbAlphaBeta.TabStop = true;
            rbAlphaBeta.Text = "Альфа-бета";
            rbAlphaBeta.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Font = new Font("Segoe UI", 12F);
            lblStatus.Location = new Point(14, 369);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(259, 39);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Вы:";
            // 
            // pnlBoard
            // 
            pnlBoard.Dock = DockStyle.Right;
            pnlBoard.Location = new Point(330, 0);
            pnlBoard.Name = "pnlBoard";
            pnlBoard.Size = new Size(480, 480);
            pnlBoard.TabIndex = 3;
            // 
            // btnNewGame
            // 
            btnNewGame.Location = new Point(14, 426);
            btnNewGame.Name = "btnNewGame";
            btnNewGame.Size = new Size(259, 34);
            btnNewGame.TabIndex = 4;
            btnNewGame.Text = "Новая игра";
            btnNewGame.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(810, 480);
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
            ((System.ComponentModel.ISupportInitialize)nudSims).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDepth).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private RadioButton rbReversi;
        private RadioButton rbCheckers;
        private GroupBox groupBox2;
        private RadioButton rbMonteCarlo;
        private RadioButton rbAlphaBeta;
        private Label label1;
        private Label label2;
        private NumericUpDown nudDepth;
        private NumericUpDown nudSims;
        private Label lblStatus;
        private Panel pnlBoard;
        private Button btnNewGame;
    }
}
