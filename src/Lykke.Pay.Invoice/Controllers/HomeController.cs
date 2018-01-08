﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Lykke.Pay.Invoice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Lykke.Pay.Service.Invoces.Client;
using System.Linq;
using Common;
using Common.Log;
using Lykke.Pay.Common;
using Lykke.Pay.Invoice.AppCode;
using Lykke.Pay.Invoice.Clients.MerchantAuth;
using Lykke.Pay.Invoice.Clients.MerchantAuth.Models;
using PagedList;
using Lykke.Pay.Service.Invoces.Client.Models.Invoice;
using NewInvoiceModel = Lykke.Pay.Invoice.Models.NewInvoiceModel;

namespace Lykke.Pay.Invoice.Controllers
{
    [Authorize]
    [Route("home")]
    public class HomeController : BaseController
    {
        private readonly IPayInvoicesServiceClient _invoicesServiceClient;
        private readonly IMerchantAuthServiceClient _merchantAuthServiceClient;
        private readonly ILog _log;

        public HomeController(AppSettings settings,
            IPayInvoicesServiceClient invoicesServiceClient,
            IMerchantAuthServiceClient merchantAuthServiceClient,
            ILog log)
            : base(settings)
        {
            _invoicesServiceClient = invoicesServiceClient;
            _merchantAuthServiceClient = merchantAuthServiceClient;
            _log = log;
        }

        [HttpGet("Profile")]
        public IActionResult Profile()
        {
            return View();
        }

        [HttpPost("Profile")]
        public async Task<IActionResult> Profile(NewInvoiceModel model)
        {
            InvoiceModel invoice;

            try
            {
                if (model.Status == "Unpaid")
                {
                    if (string.IsNullOrEmpty(model.InvoiceNumber) || string.IsNullOrEmpty(model.ClientEmail) || string.IsNullOrEmpty(model.Amount))
                    {
                        return View();
                    }

                    invoice = await _invoicesServiceClient.GenerateInvoiceAsync(new Service.Invoces.Client.Models.Invoice.NewInvoiceModel
                    {
                        MerchantId = MerchantId,
                        InvoiceNumber = model.InvoiceNumber,
                        ClientName = model.ClientName,
                        ClientEmail = model.ClientEmail,
                        Amount = double.Parse(model.Amount, CultureInfo.InvariantCulture),
                        Currency = model.Currency,
                        DueDate = DateTime.Parse(model.StartDate, CultureInfo.InvariantCulture)
                            .Add(OrderLiveTime)
                            .RepoDateStr()
                    });
                }
                else if (model.Status == "Draft")
                {
                    invoice = await _invoicesServiceClient.CreateDraftInvoiceAsync(new NewDraftInvoiceModel
                    {
                        MerchantId = MerchantId,
                        InvoiceNumber = model.InvoiceNumber,
                        ClientName = model.ClientName,
                        ClientEmail = model.ClientEmail,
                        Amount = double.Parse(model.Amount, CultureInfo.InvariantCulture),
                        Currency = model.Currency,
                        DueDate = DateTime.Parse(model.StartDate, CultureInfo.InvariantCulture)
                            .Add(OrderLiveTime)
                            .RepoDateStr()
                    });
                }
                else
                    throw new InvalidOperationException("Unknown action");
            }
            catch (Exception exception)
            {
                await _log.WriteErrorAsync(nameof(HomeController), nameof(Profile), model.ToJson(), exception);
                return BadRequest("Cannot create invoce!");
            }

            ViewBag.GeneratedItem = JsonConvert.SerializeObject(invoice);

            return View();
        }

        [HttpPost("Balance")]
        public async Task<JsonResult> Balance()
        {
            string asset = "CHF";
            double amount = 0d;

            // TODO: Move to base controller
            string id = User.Claims.First(u => u.Type == ClaimTypes.Sid).Value;

            try
            {
                MerchantBalanceResponse response = await _merchantAuthServiceClient.GetBalanceAsync(id);

                MerchantAssetBalance assetBalance = response.Assets?.FirstOrDefault(o => o.Asset == asset);

                if (assetBalance != null)
                    amount = assetBalance.Value;
            }
            catch (Exception exception)
            {
                await _log.WriteErrorAsync(nameof(HomeController), nameof(Balance), MerchantId, exception);
            }

            return Json($"{amount:0.00}  {asset}");
        }

        [HttpGet("InvoiceDetail")]
        public async Task<IActionResult> InvoiceDetail(string invoiceId)
        {
            var model = new InvoiceDetailModel
            {
                Data = await _invoicesServiceClient.GetInvoiceAsync(MerchantId, invoiceId)
            };

            if (model.Data.Status != InvoiceStatus.Paid.ToString())
            {
                model.InvoiceUrl = $"{SiteUrl.TrimEnd('/')}/invoice/{model.Data.InvoiceId}";
                model.QRCode = $@"https://chart.googleapis.com/chart?chs=220x220&chld=L|2&cht=qr&chl={model.InvoiceUrl}";
            }

            return View(model);
        }

        [HttpPost("InvoiceDetail")]
        public async Task<IActionResult> InvoiceDetail(InvoiceDetailModel model, string generate, string draft, string delete)
        {
            try
            {
                if (model.Data.Status == "Unpaid")
                {
                    if (string.IsNullOrEmpty(model.Data.InvoiceNumber) || string.IsNullOrEmpty(model.Data.ClientEmail) || model.Data.Amount < .1)
                    {
                        // TODO: Need to change invoice process model
                        return RedirectToAction("Profile");
                    }

                    await _invoicesServiceClient.GenerateInvoiceFromDraftAsync(new UpdateInvoiceModel
                    {
                        MerchantId = MerchantId,
                        InvoiceId = model.Data.InvoiceId,
                        InvoiceNumber = model.Data.InvoiceNumber,
                        ClientName = model.Data.ClientName,
                        ClientEmail = model.Data.ClientEmail,
                        Amount = model.Data.Amount,
                        Currency = model.Data.Currency,
                        DueDate = DateTime.Parse(model.Data.DueDate, CultureInfo.InvariantCulture)
                            .RepoDateStr()
                    });
                } else if (model.Data.Status == "Draft")
                {
                    await _invoicesServiceClient.UpdateDraftInvoiceAsync(new UpdateDraftInvoiceModel
                    {
                        MerchantId = MerchantId,
                        InvoiceId = model.Data.InvoiceId,
                        InvoiceNumber = model.Data.InvoiceNumber,
                        ClientName = model.Data.ClientName,
                        ClientEmail = model.Data.ClientEmail,
                        Amount = model.Data.Amount,
                        Currency = model.Data.Currency,
                        DueDate = DateTime.Parse(model.Data.DueDate, CultureInfo.InvariantCulture)
                            .RepoDateStr()
                    });
                }
                else if (model.Data.Status == "Remove")
                {
                    await _invoicesServiceClient.DeleteInvoiceAsync(MerchantId, model.Data.InvoiceId);
                    return RedirectToAction("Profile");
                }
                else
                {
                    throw new InvalidOperationException("Unknown action");
                }
            }
            catch (Exception exception)
            {
                await _log.WriteErrorAsync(nameof(HomeController), nameof(InvoiceDetail), model.ToJson(), exception);
                return BadRequest("Error processing invoce!");
            }

            return RedirectToAction("InvoiceDetail", new
            {
                invoiceId = model.Data.InvoiceId
            });
        }

        [HttpPost("Invoices")]
        public async Task<JsonResult> Invoices(GridModel model)
        {
            var respmodel = new GridModel();
            IEnumerable<InvoiceModel> invoices = await _invoicesServiceClient.GetInvoicesAsync(MerchantId);
            var orderedlist = invoices.OrderByDescending(i => i.StartDate).ToList();

            if (model.Filter.Status != "All")
            {
                orderedlist = orderedlist.Where(i => i.Status == model.Filter.Status).ToList();
            }

            if (!string.IsNullOrEmpty(model.Filter.SearchValue))
            {
                orderedlist = orderedlist.Where(i =>
                        i.ClientEmail != null && i.ClientEmail.Contains(model.Filter.SearchValue)
                        ||
                        i.InvoiceNumber != null && i.InvoiceNumber.Contains(model.Filter.SearchValue))
                    .OrderByDescending(i => i.StartDate)
                    .ToList();
            }

            if (model.Filter.Status == "All")
            {
                respmodel.Header.AllCount = orderedlist.Count;
                respmodel.Header.DraftCount = orderedlist.Count(i => i.Status == InvoiceStatus.Draft.ToString());
                respmodel.Header.PaidCount = orderedlist.Count(i => i.Status == InvoiceStatus.Paid.ToString());
                respmodel.Header.UnpaidCount = orderedlist.Count(i => i.Status == InvoiceStatus.Unpaid.ToString());
                respmodel.Header.RemovedCount = orderedlist.Count(i => i.Status == InvoiceStatus.Removed.ToString());
                respmodel.Header.InProgressCount =
                    orderedlist.Count(i => i.Status == InvoiceStatus.InProgress.ToString());
                respmodel.Header.LatePaidCount = orderedlist.Count(i => i.Status == InvoiceStatus.LatePaid.ToString());
                respmodel.Header.UnderpaidCount =
                    orderedlist.Count(i => i.Status == InvoiceStatus.Underpaid.ToString());
                respmodel.Header.OverpaidCount = orderedlist.Count(i => i.Status == InvoiceStatus.Overpaid.ToString());
            }
            respmodel.Filter.Status = model.Filter.Status;
            if (!string.IsNullOrEmpty(model.Filter.SortField))
            {
                switch (model.Filter.SortField)
                {
                    case "number":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.InvoiceNumber).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.InvoiceNumber).ThenByDescending(i => i.StartDate)
                                .ToList();
                        break;
                    case "client":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.ClientName).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.ClientName).ThenByDescending(i => i.StartDate)
                                .ToList();
                        break;
                    case "amount":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.Amount).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.Amount).ThenByDescending(i => i.StartDate).ToList();
                        break;
                    case "currency":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.Currency).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.Currency).ThenByDescending(i => i.StartDate)
                                .ToList();
                        break;
                    case "status":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.Status).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.Status).ThenByDescending(i => i.StartDate).ToList();
                        break;
                    case "duedate":
                        orderedlist = model.Filter.SortWay == 0
                            ? orderedlist.OrderBy(i => i.DueDate).ThenByDescending(i => i.StartDate).ToList()
                            : orderedlist.OrderByDescending(i => i.DueDate).ThenByDescending(i => i.StartDate).ToList();
                        break;
                }
            }
            var period = DateTime.Now;
            switch (model.Filter.Period)
            {
                case 1:
                    period = period.AddDays(-30);
                    break;
                case 2:
                    period = period.AddDays(-60);
                    break;
                case 3:
                    period = period.AddDays(-90);
                    break;
            }
            if (period != DateTime.Now)
                orderedlist = orderedlist.Where(i => i.StartDate.GetRepoDateTime() <= period).ToList();
            respmodel.PageCount = orderedlist.Count / 20;
            respmodel.Data = orderedlist.ToPagedList(model.Page, 20).ToList();
            return Json(respmodel);
        }

        [HttpGet("DeleteInvoice")]
        public async Task<EmptyResult> DeleteInvoice(string invoiceId)
        {
            await _invoicesServiceClient.DeleteInvoiceAsync(MerchantId, invoiceId);
            return new EmptyResult();
        }
    }
}
