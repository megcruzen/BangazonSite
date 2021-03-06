﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Bangazon.Data;
using Bangazon.Models;
using Microsoft.AspNetCore.Identity;
using Bangazon.Models.OrderViewModels;
using Microsoft.AspNetCore.Authorization;

namespace Bangazon.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private Task<ApplicationUser> GetCurrentUserAsync() => _userManager.GetUserAsync(HttpContext.User);

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return NotFound();
            }
            var userid = user.Id;
            var applicationDbContext = _context.Order
                .Include(o => o.PaymentType)
                .Include(o => o.User)
                .Where(o => o.UserId == userid && o.PaymentTypeId != null);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Orders/Cart
        public async Task<IActionResult> Cart()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return NotFound();
            }
            var userid = user.Id;
            var applicationDbContext = _context.Order
                .Include(o => o.PaymentType)
                .Include(o => o.User)
                .Include(o => o.OrderProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => o.UserId == userid && o.PaymentTypeId == null);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return NotFound();
            }

            var order = await _context.Order
                .Include(o => o.PaymentType)
                .Include(o => o.User)
                .Include(o => o.OrderProducts)
                    .ThenInclude(op => op.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null || order.UserId != user.Id)
            {
                return NotFound();
            }

            return View(order);
        }

        // CREATE: Add Product to Cart
        [Authorize]
        public async Task<IActionResult> AddToCart([FromRoute] int id)
        {
            // Find the product requested
            Product productToAdd = await _context.Product.SingleOrDefaultAsync(p => p.ProductId == id);

            var user = await GetCurrentUserAsync();

            // See if the user has an open order
            var openOrder = await _context.Order.SingleOrDefaultAsync(o => o.User == user && o.PaymentTypeId == null);

            // If no order, create one, else add to existing order
            if (openOrder == null)
            {
                // Create new order
                var order = new Order();
                order.UserId = user.Id;
                _context.Add(order);

                // Add product to order, i.e. create OrderProduct
                var orderProduct = new OrderProduct();
                orderProduct.ProductId = productToAdd.ProductId;
                orderProduct.OrderId = order.OrderId;
                _context.Add(orderProduct);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Add product to existing order, i.e. create OrderProduct
                var orderProduct = new OrderProduct();
                orderProduct.ProductId = productToAdd.ProductId;
                orderProduct.OrderId = openOrder.OrderId;
                _context.Add(orderProduct);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Cart));
        }

        // DELETE: Remove Product From Cart
        [Authorize]
        public async Task<IActionResult> RemoveFromCart([FromRoute] int id)
        {
            // Find the orderProduct requested
            OrderProduct orderProductToRemove = await _context.OrderProduct.SingleOrDefaultAsync(op => op.OrderProductId == id);

            // Delete OrderProduct
            _context.OrderProduct.Remove(orderProductToRemove);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Cart));
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Order.FindAsync(id);
            if (order == null || order.PaymentTypeId != null)
            {
                return NotFound();
            }

            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return NotFound();
            }
            ViewData["PaymentTypeId"] = new SelectList(_context.PaymentType.Where(p => p.UserId == user.Id), "PaymentTypeId", "PaymentMethod");
            return View(order);
            
        }

        // Order Confirmation
        public IActionResult OrderConfirm()
        {
            return View();
        }

        // POST: Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderId,DateCreated,DateCompleted,UserId,PaymentTypeId")] Order order)
        {
            if (id != order.OrderId)
            {
                return NotFound();
            }

            ModelState.Remove("User");
            ModelState.Remove("userId");
            var user = await GetCurrentUserAsync();
            order.UserId = user.Id;

            DateTime today = DateTime.UtcNow;
            order.DateCompleted = today;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.OrderId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(OrderConfirm));
            }
            
            return View(order);
        }


        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Order
                .Include(o => o.PaymentType)
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            var user = await GetCurrentUserAsync();

            if (order == null || order.UserId != user.Id || order.PaymentTypeId != null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Order.FindAsync(id);
            var orderProducts = _context.OrderProduct;
            foreach (OrderProduct item in orderProducts) {
                if (item.OrderId == order.OrderId)
                {
                    orderProducts.Remove(item);
                }
            }
            _context.Order.Remove(order);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Order.Any(e => e.OrderId == id);
        }
    }
}
