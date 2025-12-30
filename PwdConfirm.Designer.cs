namespace SACTIBACKUP
{
    partial class PwdConfirm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PwdConfirm));
            txtContrasenha = new DevExpress.XtraEditors.TextEdit();
            labelControl2 = new DevExpress.XtraEditors.LabelControl();
            btnAceptar = new Button();
            ((System.ComponentModel.ISupportInitialize)txtContrasenha.Properties).BeginInit();
            SuspendLayout();
            // 
            // txtContrasenha
            // 
            txtContrasenha.Location = new Point(12, 35);
            txtContrasenha.Name = "txtContrasenha";
            txtContrasenha.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.False;
            txtContrasenha.Properties.Appearance.BorderColor = Color.FromArgb(224, 224, 224);
            txtContrasenha.Properties.Appearance.Font = new Font("Tahoma", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtContrasenha.Properties.Appearance.Options.UseBorderColor = true;
            txtContrasenha.Properties.Appearance.Options.UseFont = true;
            txtContrasenha.Properties.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            txtContrasenha.Properties.PasswordChar = '*';
            txtContrasenha.Size = new Size(248, 26);
            txtContrasenha.TabIndex = 6;
            // 
            // labelControl2
            // 
            labelControl2.Appearance.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            labelControl2.Appearance.Options.UseFont = true;
            labelControl2.Location = new Point(12, 12);
            labelControl2.Name = "labelControl2";
            labelControl2.Size = new Size(83, 20);
            labelControl2.TabIndex = 5;
            labelControl2.Text = "Contraseña";
            // 
            // btnAceptar
            // 
            btnAceptar.Font = new Font("Microsoft Sans Serif", 12F);
            btnAceptar.Image = Properties.Resources.icons8_aceptar_48__1_;
            btnAceptar.ImageAlign = ContentAlignment.MiddleLeft;
            btnAceptar.Location = new Point(158, 67);
            btnAceptar.Name = "btnAceptar";
            btnAceptar.Size = new Size(102, 32);
            btnAceptar.TabIndex = 19;
            btnAceptar.Text = "Aceptar";
            btnAceptar.TextAlign = ContentAlignment.MiddleRight;
            btnAceptar.UseVisualStyleBackColor = true;
            // 
            // PwdConfirm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(272, 118);
            Controls.Add(btnAceptar);
            Controls.Add(txtContrasenha);
            Controls.Add(labelControl2);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(288, 157);
            Name = "PwdConfirm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Autorización";
            ((System.ComponentModel.ISupportInitialize)txtContrasenha.Properties).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DevExpress.XtraEditors.TextEdit txtContrasenha;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private Button btnAceptar;
    }
}