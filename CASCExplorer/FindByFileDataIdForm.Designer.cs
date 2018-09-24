namespace CASCExplorer
{
    partial class FindByFileDataIdForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.searchTextBox = new System.Windows.Forms.TextBox();
            this.resultTextBox = new System.Windows.Forms.TextBox();
            this.findButton = new System.Windows.Forms.Button();
            this.fileDataIdLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // searchTextBox
            // 
            this.searchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchTextBox.Location = new System.Drawing.Point(74, 12);
            this.searchTextBox.Name = "searchTextBox";
            this.searchTextBox.Size = new System.Drawing.Size(94, 20);
            this.searchTextBox.TabIndex = 0;
            // 
            // resultTextBox
            // 
            this.resultTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.resultTextBox.Location = new System.Drawing.Point(15, 38);
            this.resultTextBox.Name = "resultTextBox";
            this.resultTextBox.Size = new System.Drawing.Size(225, 20);
            this.resultTextBox.TabIndex = 1;
            // 
            // findButton
            // 
            this.findButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.findButton.Location = new System.Drawing.Point(174, 10);
            this.findButton.Name = "findButton";
            this.findButton.Size = new System.Drawing.Size(66, 23);
            this.findButton.TabIndex = 2;
            this.findButton.Text = "Find";
            this.findButton.UseVisualStyleBackColor = true;
            this.findButton.Click += new System.EventHandler(this.findButton_Click);
            // 
            // fileDataIdLabel
            // 
            this.fileDataIdLabel.AutoSize = true;
            this.fileDataIdLabel.Location = new System.Drawing.Point(12, 15);
            this.fileDataIdLabel.Name = "fileDataIdLabel";
            this.fileDataIdLabel.Size = new System.Drawing.Size(58, 13);
            this.fileDataIdLabel.TabIndex = 3;
            this.fileDataIdLabel.Text = "FileDataId:";
            // 
            // FindByFileDataIdForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(250, 77);
            this.Controls.Add(this.fileDataIdLabel);
            this.Controls.Add(this.findButton);
            this.Controls.Add(this.resultTextBox);
            this.Controls.Add(this.searchTextBox);
            this.Name = "FindByFileDataIdForm";
            this.Text = "Find by FileDataID";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox searchTextBox;
        private System.Windows.Forms.TextBox resultTextBox;
        private System.Windows.Forms.Button findButton;
        private System.Windows.Forms.Label fileDataIdLabel;
    }
}