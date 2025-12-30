
// Domain/BackupConfig.cs
#nullable enable
using System;
using System.Collections.Generic;

namespace SACTIBACKUP.Domain
{
    public sealed class BackupConfig
    {
        public string RutaRespaldo { get; set; } = string.Empty;
        public string PasswordArchivos { get; set; } = string.Empty;
        public string PasswordConfirm { get; set; } = string.Empty;

        public string CorreoFTP { get; set; } = string.Empty;
        public string PasswordFTP { get; set; } = string.Empty;

        public string HoraRespaldo { get; set; } = "00:00:00";
        public string AliasGlobal { get; set; } = string.Empty;
        public string CorreoNotificaciones { get; set; } = string.Empty;

        public bool RespaldarSQL { get; set; }
        public bool RespaldarFireBird { get; set; }
        public bool RespaldarNube { get; set; }
        public bool BDComprimidas { get; set; }
        public bool UsarRazonSocial { get; set; }

        public List<string> ListaRutaFireBird { get; set; } = new();
        public List<string> ListaInstanciasSQL { get; set; } = new();

        public DateTime? FechaUltimoRespaldo { get; set; }
        public DateTime? FechaUltimoRespaldoNube { get; set; }

        // Datos de licencia (opcional)
        public string NombreEmpresa { get; set; } = string.Empty;
        public string BorrarRespaldo { get; set; } = string.Empty;
        public string DiasRespaldo { get; set; } = string.Empty;
        public string NumeroRespaldos { get; set; } = string.Empty;
        public string CorreoReceptorCliente { get; set; } = string.Empty;
        public DateTime? FechaInicioLicencia { get; set; }
        public DateTime? FechaFinLicencia { get; set; }
        public bool LicenciaVencida { get; set; }
    }
}
