﻿

using Lykke.Pay.Invoice.AppCode;
using Lykke.Pay.Service.Invoces.Client.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Pay.Common;

namespace Lykke.Pay.Invoice.Models
{
    public class InvoiceRequest
    {
        public string InvoiceNumber { get; set; }
        public string InvoiceId { get; set; }

        public string ClientName { get; set; }

        public string ClientEmail { get; set; }

        public string Amount { get; set; }
        public string Currency { get; set; }

        public string Label { get; set; }

        public string DueDate { get; set; }
        public string Status { get; set; }
        public string WalletAddress { get; set; }
        public string StartDate { get; set; }
        public string ClientId { get; set; }
        public string ClientUserId { get; set; }

        public InvoiceEntity CreateEntity(string merchantId)
        {
            if (string.IsNullOrEmpty(ClientName))
            {
                return null;
            }
            var invoiceid = string.IsNullOrEmpty(InvoiceId) ? Guid.NewGuid().ToString() : InvoiceId;
            if (string.IsNullOrEmpty(Amount))
                Amount = "0";
            StartDate = StartDate ?? DateTime.Now.RepoDateStr();
            var dueDate = DateTime.Parse(StartDate, CultureInfo.InvariantCulture);
            dueDate = dueDate.AddDays(1).AddSeconds(-1);
            return new InvoiceEntity
            {
                Amount = double.Parse(Amount, CultureInfo.InvariantCulture),
                ClientEmail = ClientEmail,
                ClientName = ClientName,
                Currency = Currency,
                InvoiceNumber = InvoiceNumber,
                InvoiceId = invoiceid,
                Label = Label,
                Status = Status,
                WalletAddress = WalletAddress,
                StartDate = DateTime.Now.RepoDateStr(),
                DueDate = dueDate.RepoDateStr(),
                MerchantId = merchantId
            };
        }

        public void BindEntity(IInvoiceEntity data)
        {
            Status = data.Status;
            Amount = data.Amount.ToString();
            ClientEmail = data.ClientEmail;
            ClientId = data.ClientId;
            ClientName = data.ClientName;
            ClientUserId = data.ClientUserId;
            Currency = data.Currency;
            DueDate = data.DueDate;
            InvoiceId = data.InvoiceId;
            InvoiceNumber = data.InvoiceNumber;
            StartDate = data.StartDate;
            WalletAddress = data.WalletAddress;
        }
    }
}