using System.Text.Json.Serialization;

namespace PlataformaIncidencias.Models
{
    public class Incidencia
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Estacion")]
        public string Estacion { get; set; } = "";

        [JsonPropertyName("Descripcion")]
        public string Descripcion { get; set; } = "";

        [JsonPropertyName("Prioridad")]
        public string Prioridad { get; set; } = "";

        [JsonPropertyName("Estado")]
        public string Estado { get; set; } = "";
    }
}