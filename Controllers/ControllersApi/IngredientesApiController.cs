using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiRoti.Data;
using MiRoti.Models;

namespace MiRoti.ControllersApi
{
    [ApiController]
    [Route("api/ingredientes")]
    // 🔒 Sólo el rol nuevo y el Admin; el string debe coincidir EXACTO con Usuario.Rol
    [Authorize(Roles = "Administrador de Insumos,Admin")]
    public class IngredientesApiController : ControllerBase
    {
        private readonly MiRotiContext _context;

        public IngredientesApiController(MiRotiContext context)
        {
            _context = context;
        }

        // ✅ GET original — intacto
        [HttpGet]
        public async Task<IActionResult> GetIngredientes()
        {
            var ingredientes = await _context.Ingredientes
                .Include(i => i.UnidadMedida)
                .Select(i => new {
                    i.Id,
                    i.Nombre,
                    i.CostoUnitario,
                    UnidadMedida = new {
                        i.UnidadMedida.Id,
                        i.UnidadMedida.Nombre,
                        i.UnidadMedida.Abreviatura
                    }
                })
                .ToListAsync();

            return Ok(ingredientes);
        }

        // ➕ NUEVO: cargar ingrediente nuevo
        [HttpPost]
        public async Task<IActionResult> CrearIngrediente([FromBody] IngredienteCrearRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Nombre))
                return BadRequest(new { mensaje = "El nombre es obligatorio." });
            if (request.CostoUnitario < 0)
                return BadRequest(new { mensaje = "El precio no puede ser negativo." });

            // Resolver la unidad por abreviatura ("kg", "g", "u", "L")
            var abrev = request.UnidadMedida?.Trim().ToLower() ?? "";
            var unidad = await _context.UnidadesMedida
                .FirstOrDefaultAsync(u => u.Abreviatura.ToLower() == abrev);
            if (unidad == null)
                return BadRequest(new { mensaje = "Unidad no encontrada. Usá kg, g, u o L." });

            var nombre = request.Nombre.Trim();
            if (await _context.Ingredientes.AnyAsync(i => i.Nombre.ToLower() == nombre.ToLower()))
                return Conflict(new { mensaje = "Ya existe un ingrediente con ese nombre." });

            var ingrediente = new Ingrediente
            {
                Nombre = nombre,
                CostoUnitario = request.CostoUnitario,
                UnidadMedidaId = unidad.Id
            };

            _context.Ingredientes.Add(ingrediente);
            await _context.SaveChangesAsync();

            return Ok(new { ingrediente.Id, ingrediente.Nombre, ingrediente.CostoUnitario });
        }

        // 💲 NUEVO: corregir precio (uso principal del rol)
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarPrecio(int id, [FromBody] IngredientePrecioRequest request)
        {
            if (request.CostoUnitario < 0)
                return BadRequest(new { mensaje = "El precio no puede ser negativo." });

            var ingrediente = await _context.Ingredientes.FindAsync(id);
            if (ingrediente == null)
                return NotFound(new { mensaje = "Ingrediente no encontrado." });

            ingrediente.CostoUnitario = request.CostoUnitario;
            await _context.SaveChangesAsync();

            return Ok(new { ingrediente.Id, ingrediente.Nombre, ingrediente.CostoUnitario });
        }

        // 📦 NUEVO: obtener y actualizar stock
        [HttpGet("{id}/stock")]
        public async Task<IActionResult> ObtenerStock(int id)
        {
            var ingrediente = await _context.Ingredientes.FindAsync(id);
            if (ingrediente == null)
                return NotFound(new { mensaje = "Ingrediente no encontrado." });

            return Ok(new { ingrediente.Id, ingrediente.Nombre, ingrediente.StockActual });
        }

        [HttpPut("{id}/stock")]
        public async Task<IActionResult> ActualizarStock(int id, [FromBody] StockRequest request)
        {
            if (request.NuevaCantidad < 0)
                return BadRequest(new { mensaje = "El stock no puede ser negativo." });

            var ingrediente = await _context.Ingredientes.FindAsync(id);
            if (ingrediente == null)
                return NotFound(new { mensaje = "Ingrediente no encontrado." });

            ingrediente.StockActual = request.NuevaCantidad;
            await _context.SaveChangesAsync();

            return Ok(new { ingrediente.Id, ingrediente.Nombre, ingrediente.StockActual });
        }

        // DTOs de entrada (no tocan la entidad Ingrediente)
        public class IngredienteCrearRequest
        {
            public string Nombre { get; set; } = "";
            public decimal CostoUnitario { get; set; }
            public string UnidadMedida { get; set; } = ""; // abreviatura: "kg", "g", "u", "L"
        }

        public class IngredientePrecioRequest
        {
            public decimal CostoUnitario { get; set; }
        }

        public class StockRequest
        {
            public decimal NuevaCantidad { get; set; }
        }
    }
}