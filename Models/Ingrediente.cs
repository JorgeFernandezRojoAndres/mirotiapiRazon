using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiRoti.Models
{
    [Table("Ingrediente")]
    public class Ingrediente
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El costo unitario es obligatorio")]
        [Range(0.01, 999999.99, ErrorMessage = "El costo debe ser mayor a 0")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal CostoUnitario { get; set; }

        // 🔗 Relación con UnidadMedida
        [Required(ErrorMessage = "Debe seleccionar una unidad de medida")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una unidad de medida válida")]
        public int UnidadMedidaId { get; set; }

        [ForeignKey(nameof(UnidadMedidaId))]
        public UnidadMedida? UnidadMedida { get; set; }

        // 📦 Stock disponible
        [Column(TypeName = "decimal(10,2)")]
        public decimal StockActual { get; set; } = 0;

        // 🔗 Relación muchos a muchos con Plato
        public ICollection<PlatoIngrediente>? PlatoIngredientes { get; set; }
    }
}