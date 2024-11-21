﻿using Microsoft.AspNetCore.Mvc;
using PaymentProvider.Entities;
using PaymentProvider.Factories.Entities;
using PaymentProvider.Models;
using PaymentProvider.Models.OrderConfirmationModels;
using PaymentProvider.Services;
using Stripe.Checkout;

namespace PaymentProvider.Controllers
{
    [Route("create-checkout-session")]
    [ApiController]
    public class CheckoutController(OrderService orderService, StripeService stripeCustomerService) : ControllerBase
    {
        private readonly OrderService _orderService = orderService;
        private readonly StripeService _stripeCustomerService = stripeCustomerService;

        [HttpPost]
        public async Task<ActionResult> Create([FromBody] OrderModel orderDetails)
        {
            try
            {
                if (!ModelState.IsValid) BadRequest();
                if (orderDetails == null) return NotFound();
                var orderEntity = OrderEntityFactory.Create(orderDetails);
                await _orderService.CreateOrderAsync(orderEntity);
                var domain = "http://localhost:5173";
                var customer = _stripeCustomerService.CreateCustomer(orderDetails);
                var options = new SessionCreateOptions
                {
                    UiMode = "embedded",
                    Currency = "sek",
                    LineItems = _orderService.GetOrderItemsList(orderDetails),
                    Mode = "payment",
                    ReturnUrl = domain + "/return?session_id={CHECKOUT_SESSION_ID}",
                    Customer = customer.Id,
                    ShippingOptions =
                    [
                        _orderService.GetShippingOption(orderDetails)
                    ],
                    InvoiceCreation = new SessionInvoiceCreationOptions
                    {
                        Enabled = true
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        {"orderId", $"{orderEntity.Id}" }
                    },
                    BillingAddressCollection = "required",
                };
                var service = new SessionService();
                var session = service.Create(options);
                session.Customer = customer;
                orderEntity.SessionId = session.Id;
                await _orderService.SaveChangesAsync();
                return Ok(new { sessionId = session.Id, clientSecret = session.ClientSecret });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return BadRequest(ex.Message);
            }
        }
    }
}