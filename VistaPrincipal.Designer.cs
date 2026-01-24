namespace SACTIBACKUP
{
    partial class VistaPrincipal
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VistaPrincipal));
            panel1 = new Panel();
            panelConfiguraciones = new Panel();
            panelRespaldoEnProgreso = new Panel();
            lblEstadoRespaldo = new Label();
            progressBarRespaldo = new ProgressBar();
            notifyIcon1 = new NotifyIcon(components);
            panelRespaldoEnProgreso.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.BackColor = Color.Transparent;
            panel1.BackgroundImage = (Image)resources.GetObject("panel1.BackgroundImage");
            panel1.Location = new Point(132, 161);
            panel1.Name = "panel1";
            panel1.Size = new Size(93, 97);
            panel1.TabIndex = 0;
            //
            // panelConfiguraciones
            //
            panelConfiguraciones.AutoSize = true;
            panelConfiguraciones.BackColor = Color.Transparent;
            panelConfiguraciones.BackgroundImage = (Image)resources.GetObject("panelConfiguraciones.BackgroundImage");
            panelConfiguraciones.BackgroundImageLayout = ImageLayout.Zoom;
            panelConfiguraciones.Location = new Point(12, 21);
            panelConfiguraciones.Name = "panelConfiguraciones";
            panelConfiguraciones.Size = new Size(131, 38);
            panelConfiguraciones.TabIndex = 1;
            //
            // panelRespaldoEnProgreso
            //
            panelRespaldoEnProgreso.BackColor = Color.FromArgb(41, 128, 185);
            panelRespaldoEnProgreso.Controls.Add(progressBarRespaldo);
            panelRespaldoEnProgreso.Controls.Add(lblEstadoRespaldo);
            panelRespaldoEnProgreso.Dock = DockStyle.Bottom;
            panelRespaldoEnProgreso.Location = new Point(0, 378);
            panelRespaldoEnProgreso.Name = "panelRespaldoEnProgreso";
            panelRespaldoEnProgreso.Padding = new Padding(10);
            panelRespaldoEnProgreso.Size = new Size(349, 60);
            panelRespaldoEnProgreso.TabIndex = 2;
            panelRespaldoEnProgreso.Visible = false;
            //
            // lblEstadoRespaldo
            //
            lblEstadoRespaldo.Dock = DockStyle.Top;
            lblEstadoRespaldo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblEstadoRespaldo.ForeColor = Color.White;
            lblEstadoRespaldo.Location = new Point(10, 10);
            lblEstadoRespaldo.Name = "lblEstadoRespaldo";
            lblEstadoRespaldo.Size = new Size(329, 20);
            lblEstadoRespaldo.TabIndex = 0;
            lblEstadoRespaldo.Text = "Realizando respaldo, por favor espere...";
            lblEstadoRespaldo.TextAlign = ContentAlignment.MiddleCenter;
            //
            // progressBarRespaldo
            //
            progressBarRespaldo.Dock = DockStyle.Bottom;
            progressBarRespaldo.Location = new Point(10, 35);
            progressBarRespaldo.Maximum = 100;
            progressBarRespaldo.Minimum = 0;
            progressBarRespaldo.Name = "progressBarRespaldo";
            progressBarRespaldo.Size = new Size(329, 15);
            progressBarRespaldo.Style = ProgressBarStyle.Continuous;
            progressBarRespaldo.TabIndex = 1;
            progressBarRespaldo.Value = 0;
            //
            // notifyIcon1
            // 
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "SACTI Backup";
            notifyIcon1.Visible = true;
            // 
            // VistaPrincipal
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            ClientSize = new Size(349, 438);
            Controls.Add(panelConfiguraciones);
            Controls.Add(panel1);
            Controls.Add(panelRespaldoEnProgreso);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MaximumSize = new Size(365, 477);
            MinimumSize = new Size(365, 477);
            Name = "VistaPrincipal";
            StartPosition = FormStartPosition.CenterScreen;
            Resize += VistaPrincipal_Resize;
            panelRespaldoEnProgreso.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel panel1;
        private Panel panelConfiguraciones;
        private Panel panelRespaldoEnProgreso;
        private Label lblEstadoRespaldo;
        private ProgressBar progressBarRespaldo;
        private NotifyIcon notifyIcon1;
    }
}
