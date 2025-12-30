using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SACTIBACKUP.Entidades
{
    public class Backup
    {
        public string? Usuario { get; set; }
        public string? Password { get; set; }
        public bool RespaldarSQL { get; set; }
        public bool RespaldarFireBird { get; set; }
        public string? RutaRespaldo { get; set; }
        public string? PasswordArchivos { get; set; }
        public string? ConfirmacionPasswordArchivos { get; set; }
        public string? AliasGlobal { get; set; }
        public string? Correo { get; set; }
        public string? HoraRespaldo { get; set; }
        public bool RespaldarNube { get; set; }
        public List<string>? ListaRutasFirebird { get; set; }
        public List<string>? ListaInstanciasSQL { get; set; }

    }
}


