using System.ComponentModel.DataAnnotations;

namespace PlataformaIncidencias.Models
{
    public class Incidencia
    {
        public int Id { get; set; }

        [Required]
        public string Estacion { get; set; } = string.Empty;

        [Required]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        public string Prioridad { get; set; } = "Media";

        [Required]
        public string Estado { get; set; } = "Abierta";
    }
}