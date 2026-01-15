using DevExpress.XtraEditors;
//using Sacti.FrameworkUI.Librerias;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SACTIBACKUP
{
    public partial class PwdConfirm : Form
    {
        public string pwd { get; set; }
        public bool pwdCorrecta { get; set; }
        public PwdConfirm(string _pwd)
        {
            InitializeComponent();
            pwd = _pwd;


            btnAceptar.Click += BtnAceptar_Click;
            txtContrasenha.KeyDown += TxtContrasenha_KeyDown;

        }

        private void TxtContrasenha_KeyDown(object? sender, KeyEventArgs e)
        {
            pwdCorrecta = false;
            if (e.KeyCode == Keys.Enter)
            {
                ValidarContrasenha();
            }
            
        }

        private void BtnAceptar_Click(object? sender, EventArgs e)
        {
            pwdCorrecta = false;
            ValidarContrasenha();
        }

        public void ValidarContrasenha()
        {
            if (txtContrasenha.Text.Trim() != pwd.Trim())
            {
                MessageBox.Show("Contraseña Incorrecta.");
            }
            else
            {
                pwdCorrecta = true;
                this.Close();
            }
              
        }
    }
}
