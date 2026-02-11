using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiRoti.Data;
using MiRoti.Models;
using MiRoti.DTOs;
using System;
using System.Linq;
using System.Security.Claims;

namespace MiRoti.ControllersApi
{
    [ApiController]
    [Route("api/pedidos")]
    [Authorize] // 🔐 Protege todas las rutas (requiere token JWT)
    public class PedidosApiController : ControllerBase
    {
        private readonly MiRotiContext _context;

        public PedidosApiController(MiRotiContext context)
        {
            _context = context;
        }

        // DTOs para crear pedido sin requerir la entidad completa
        public class DetalleCreateDto
        {
            public int PlatoId { get; set; }
            public int Cantidad { get; set; }
            public decimal Subtotal { get; set; }
        }

        public class PedidoCreateDto
        {
            public decimal Total { get; set; }
            public List<DetalleCreateDto> Detalles { get; set; } = new();
        }

        // ===========================================================
        // ✅ GET: api/pedidos (solo Admin)
        // ===========================================================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPedidos()
        {
            var pedidosQuery = _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Cadete)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Plato)
                .OrderByDescending(p => p.FechaHora);

            return Ok(await MapPedidos(pedidosQuery).ToListAsync());
        }

        // ===========================================================
        // ✅ POST: api/pedidos (Cliente crea un nuevo pedido)
        // ===========================================================
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> PostPedido([FromBody] PedidoCreateDto pedidoDto)
        {
            try
            {
                // 🧩 Obtener el ID del cliente autenticado (claim "id" o NameIdentifier)
                var clienteIdStr = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(clienteIdStr) || !int.TryParse(clienteIdStr, out var clienteId))
                    return Unauthorized(new { success = false, error = "No se pudo obtener el identificador del cliente." });

                // Asegurar que no haya pedidos activos y reemplazar el último si existe
                var estadosActivos = new[] { "Pendiente", "En Preparación", "En Camino" };
                var tienePedidoActivo = await _context.Pedidos
                    .AnyAsync(p => p.ClienteId == clienteId && estadosActivos.Contains(p.Estado));

                if (tienePedidoActivo)
                    return Conflict(new { success = false, error = "Ya existe un pedido activo" });

                var pedido = new Pedido
                {
                    ClienteId = clienteId,
                    Cliente = null!,
                    Estado = "Pendiente",
                    FechaHora = DateTime.Now,
                    Total = pedidoDto.Total,
                    Detalles = (pedidoDto.Detalles ?? new()).Select(d => new DetallePedido
                    {
                        PlatoId = d.PlatoId,
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal
                    }).ToList()
                };

                _context.Pedidos.Add(pedido);
                await _context.SaveChangesAsync();

                // 🔁 Devolver DTO del pedido recién creado
                var pedidosQuery = _context.Pedidos
                    .Where(p => p.Id == pedido.Id)
                    .Include(p => p.Cliente)
                    .Include(p => p.Cadete)
                    .Include(p => p.Detalles)
                        .ThenInclude(d => d.Plato);

                var pedidoResult = await MapPedidos(pedidosQuery).FirstOrDefaultAsync();

                return CreatedAtAction(nameof(GetPedidoById), new { id = pedido.Id }, pedidoResult);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ===========================================================
        // ✅ GET: api/pedidos/{id}
        // ===========================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPedidoById(int id)
        {
            var pedidoQuery = _context.Pedidos
                .Where(p => p.Id == id)
                .Include(p => p.Cliente)
                .Include(p => p.Cadete)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Plato);

            var pedido = await MapPedidos(pedidoQuery).FirstOrDefaultAsync();

            if (pedido == null)
                return NotFound();

            return Ok(pedido);
        }

        // ===========================================================
        // ✅ GET: api/pedidos/mis-pedidos (Cliente)
        // ===========================================================
        [HttpGet("mis-pedidos")]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> GetMisPedidos()
        {
            var clienteIdStr = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(clienteIdStr) || !int.TryParse(clienteIdStr, out var clienteId))
                return Unauthorized(new { success = false, error = "No se pudo obtener el identificador del cliente." });

            var pedidosQuery = _context.Pedidos
                .Where(p => p.ClienteId == clienteId)
                .Include(p => p.Cliente)
                .Include(p => p.Cadete)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Plato)
                .OrderByDescending(p => p.FechaHora);

            return Ok(await MapPedidos(pedidosQuery).ToListAsync());
        }

        // ===========================================================
        // ✅ GET: api/pedidos/asignados (Cadete)
        // ===========================================================
        [HttpGet("asignados")]
        [Authorize(Roles = "Cadete")]
        public async Task<IActionResult> GetPedidosAsignados()
        {
            var cadeteIdStr = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(cadeteIdStr) || !int.TryParse(cadeteIdStr, out var cadeteId))
                return Unauthorized(new { success = false, error = "No se pudo obtener el identificador del cadete." });

            var pedidos = await _context.Pedidos
                .Where(p => p.CadeteId == cadeteId && p.Estado != "Entregado")
                .Include(p => p.Cliente)
                .Include(p => p.Cadete)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Plato)
                .OrderBy(p => p.FechaHora)
                .Select(p => new PedidoDTO
                {
                    Id = p.Id,
                    Cliente = p.Cliente.Nombre,
                    ClienteDireccion = p.Cliente.Direccion ?? string.Empty,
                    ClienteTelefono = p.Cliente.Telefono ?? string.Empty,
                    Cadete = p.Cadete != null ? p.Cadete.Nombre : "Sin cadete",
                    CadeteTelefono = p.Cadete != null ? p.Cadete.Telefono : null,
                    FechaHora = p.FechaHora,
                    Estado = p.Estado,
                    Total = p.Total,
                    Detalles = p.Detalles.Select(d => new DetallePedidoDTO
                    {
                        Plato = d.Plato.Nombre,
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal,
                        ImagenUrl = d.Plato != null ? d.Plato.ImagenUrl ?? string.Empty : string.Empty
                    }).ToList()
                })
                .ToListAsync();

            return Ok(pedidos);
        }

        // ===========================================================
        // ✅ GET: api/pedidos/disponibles (Cadete)
        // ===========================================================
        [HttpGet("disponibles")]
        [Authorize(Roles = "Cadete")]
        public async Task<IActionResult> GetPedidosDisponibles()
        {
            var pedidos = await _context.Pedidos
                .Where(p => p.Estado == "Pendiente" && p.CadeteId == null)
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Plato)
                .OrderBy(p => p.FechaHora)
                .Select(p => new PedidoDTO
                {
                    Id = p.Id,
                    Cliente = p.Cliente.Nombre,
                    ClienteDireccion = p.Cliente.Direccion ?? string.Empty,
                    ClienteTelefono = p.Cliente.Telefono ?? string.Empty,
                    Cadete = "Sin cadete",
                    CadeteTelefono = null,
                    FechaHora = p.FechaHora,
                    Estado = p.Estado,
                    Total = p.Total,
                    Detalles = p.Detalles.Select(d => new DetallePedidoDTO
                    {
                        Plato = d.Plato.Nombre,
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal,
                        ImagenUrl = d.Plato != null ? d.Plato.ImagenUrl ?? string.Empty : string.Empty
                    }).ToList()
                })
                .ToListAsync();

            return Ok(pedidos);
        }

        // ===========================================================
        // ✅ PUT: api/pedidos/{id}/tomar (Cadete)
        // ===========================================================
        [HttpPut("{id}/tomar")]
        [Authorize(Roles = "Cadete")]
        public async Task<IActionResult> TomarPedido(int id)
        {
            var cadeteIdStr = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(cadeteIdStr) || !int.TryParse(cadeteIdStr, out var cadeteId))
                return Unauthorized(new { success = false, error = "No se pudo obtener el identificador del cadete." });

            var pedido = await _context.Pedidos.FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.Estado == "Pendiente" &&
                p.CadeteId == null);

            if (pedido == null)
                return NotFound(new { success = false, error = "Pedido no disponible" });

            pedido.CadeteId = cadeteId;
            pedido.Estado = "En Camino";
            await _context.SaveChangesAsync();

            var pedidoDto = await MapPedidos(_context.Pedidos
                    .Where(p => p.Id == id)
                    .Include(p => p.Cliente)
                    .Include(p => p.Cadete)
                    .Include(p => p.Detalles)
                        .ThenInclude(d => d.Plato))
                .FirstOrDefaultAsync();

            return Ok(pedidoDto);
        }

        private IQueryable<PedidoDTO> MapPedidos(IQueryable<Pedido> pedidos)
        {
            return pedidos.Select(p => new PedidoDTO
            {
                Id = p.Id,
                Cliente = p.Cliente.Nombre,
                ClienteDireccion = p.Cliente.Direccion ?? string.Empty,
                ClienteTelefono = p.Cliente.Telefono ?? string.Empty,
                Cadete = p.Cadete != null ? p.Cadete.Nombre : "Sin cadete",
                CadeteTelefono = p.Cadete != null ? p.Cadete.Telefono : null,
                FechaHora = p.FechaHora,
                Estado = p.Estado,
                Total = p.Total,
                Detalles = p.Detalles.Select(d => new DetallePedidoDTO
                {
                    Plato = d.Plato.Nombre,
                    Cantidad = d.Cantidad,
                    Subtotal = d.Subtotal,
                    ImagenUrl = d.Plato != null ? d.Plato.ImagenUrl ?? string.Empty : string.Empty
                }).ToList()
            });
        }

        // ===========================================================
        // ✅ PUT: api/pedidos/{id}/entregar (Cadete)
        // ===========================================================
        [HttpPut("{id}/entregar")]
        [Authorize(Roles = "Cadete")]
        public async Task<IActionResult> EntregarPedido(int id)
        {
            var cadeteIdStr = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(cadeteIdStr) || !int.TryParse(cadeteIdStr, out var cadeteId))
                return Unauthorized(new { success = false, error = "No se pudo obtener el identificador del cadete." });
            var pedido = await _context.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.CadeteId == cadeteId);

            if (pedido == null)
                return NotFound(new { success = false, error = "Pedido no encontrado o no asignado a este cadete" });

            if (pedido.Estado == "Entregado")
                return BadRequest(new { success = false, error = "El pedido ya fue entregado" });

            pedido.Estado = "Entregado";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, mensaje = "Pedido marcado como entregado" });
        }

        // ===========================================================
        // ✅ PUT: api/pedidos/{id}/estado (Admin o Cocinero)
        // ===========================================================
        [HttpPut("{id}/estado")]
        [Authorize(Roles = "Admin,Cocinero,Cadete")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] string nuevoEstado)
        {
            var nuevoEstadoTrimmed = (nuevoEstado?.Trim()) ?? string.Empty;

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            pedido.Estado = nuevoEstadoTrimmed;
            _context.Pedidos.Update(pedido);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, mensaje = $"Estado actualizado a {nuevoEstadoTrimmed}" });
        }
    }
}
