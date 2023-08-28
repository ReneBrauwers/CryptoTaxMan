using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Threading.Tasks;
using global::Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using ManagerApp;
using ManagerApp.Shared;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;
using Common.Models;
using FileHelpers;
using ManagerApp.Models;
using System.Text;
using ManagerApp.Services;
using static ManagerApp.Utils.Enums;

namespace ManagerApp.Pages
{
    public partial class Index
    {
        private HxGrid<CryptoTransactionRecordImport>? recordsToImportGrid;
        private HxGrid<CryptoTransactionRecord>? taxRecordGrid;
        private HxModal? recordAddModal;
        private bool isEditMode = false;
        private string InName { get; set; } = "Amount (in)";
        private string InCurrency { get; set; } = "Currency (in)";
        private string OutName { get; set; } = "Amount (out)";
        private string OutCurrency { get; set; } = "Currency (out)";

        private bool inTaxMode = false;
        private List<int> TaxYears { get; set; } = new();

        private List<ExchangeRate> allExchangeRates = new();
        private TradeAction selectedTradeAction = TradeAction.not_defined;
        private CryptoTransactionRecordImport selectedDataItem = new();
        private CryptoTransactionRecord selectedTaxDataItem = new();
        private CryptoTransactionRecordImport newDataItem = new();
        private List<CryptoTransactionRecordImport> localEditableDataItem { get; set; } = new();
        private List<CryptoTransactionRecord> localEditableTaxDataItem { get; set; } = new();

        private IEnumerable<string> TradeTypes = Enum.GetNames(typeof(TradeType)).ToList();
        private List<CryptoCollection> myCryptoCollection = new();
        private List<string> TaxTransactions = new();
        private List<CryptoTaxRecords> TaxRecords = new();
        private List<CryptoWalletCollection> WalletCollection = new();
        private bool _showLoader = false;
        private string _progressMessage = string.Empty;
        private List<Portofolio> portofolios;
        protected override void OnInitialized()
        {
            int startYear = 2015;
            while (startYear <= DateTime.UtcNow.Year)
            {
                TaxYears.Add(startYear);
                startYear++;
            }
        }

        private async Task SetTradeAction(ChangeEventArgs e)
        {
            var selectedString = e?.Value?.ToString() ?? string.Empty;
            if (!Enum.TryParse<TradeAction>(selectedString, true, out selectedTradeAction))
            {
                selectedTradeAction = TradeAction.not_defined;
            }

            SetCustomLabelNames();
            await InvokeAsync(StateHasChanged);
        }

        private Task<GridDataProviderResult<CryptoTransactionRecordImport>> ClientSideProcessingDataProvider(GridDataProviderRequest<CryptoTransactionRecordImport> request)
        {
            return Task.FromResult(request.ApplyTo(localEditableDataItem));
        }

        private async Task HandleSelectedDataItemChanged(CryptoTransactionRecordImport newSelectedDataItem)
        {
            if (selectedDataItem != null)
            {
                // TODO: add your logic to save item changes here, the item which was selected (edited) is in selectedDataItem
                await Task.Delay(200); // simulates API call (saving changes itself)
                Console.WriteLine("Saving... " + selectedDataItem);
            }

            selectedDataItem = newSelectedDataItem;
        }

        private async void HandleValidSubmit()
        {
            Console.WriteLine("HandleValidSubmit called");
            // Process the valid form
            var newSeq = localEditableDataItem.MaxBy(x => x.Sequence).Sequence + 1;
            newDataItem.Sequence = newSeq;
            localEditableDataItem.Add(newDataItem);
            await recordsToImportGrid.RefreshDataAsync();
            await recordAddModal.HideAsync();
        }

        private Task<GridDataProviderResult<CryptoTransactionRecord>> ClientSideProcessingTaxDataProvider(GridDataProviderRequest<CryptoTransactionRecord> request)
        {
            return Task.FromResult(request.ApplyTo(localEditableTaxDataItem));
        }

        private async Task HandleSelectedTaxDataItemChanged(CryptoTransactionRecord newSelectedTaxDataItem)
        {
            if (selectedTaxDataItem != null)
            {
                // TODO: add your logic to save item changes here, the item which was selected (edited) is in selectedDataItem
                await Task.Delay(200); // simulates API call (saving changes itself)
                Console.WriteLine("Saving... " + selectedTaxDataItem);
            }

            selectedTaxDataItem = newSelectedTaxDataItem;
        }

        private async Task DeleteItem(CryptoTransactionRecordImport item)
        {
            localEditableDataItem.Remove(item);
            await recordsToImportGrid.RefreshDataAsync();
        }

        private async void UploadFile(InputFileChangeEventArgs e)
        {
            //Read file into stream
            using (var ms = e.File.OpenReadStream())
            {
                using (TextReader reader = new StreamReader(ms))
                {
                    var readerText = await reader.ReadToEndAsync();
                    using (var engine = new FileHelperAsyncEngine<CryptoTransactionRecordImport>())
                    {
                        using (engine.BeginReadString(readerText))
                        {
                            //Load file data into grid
                            localEditableDataItem = new List<CryptoTransactionRecordImport>();
                            localEditableDataItem.AddRange(engine.ToList());
                            await recordsToImportGrid.RefreshDataAsync();
                            //_showLoader = true;
                            //StateHasChanged();
                            //foreach (var record in engine)
                            //{
                            //    _importedRecords.AddRange(Utils.Taxify.Flatten(record));
                            //}
                        }
                    }
                }
            }
            //await ExportRecords(_importedRecords);
            //_showLoader = false;
            //StateHasChanged();
        }

        private async Task CreateTaxRecords()
        {
            if (localEditableDataItem.Count > 0)
            {
                //Flatten
                localEditableTaxDataItem = new List<CryptoTransactionRecord>();
                _showLoader = true;
                await InvokeAsync(StateHasChanged);
                allExchangeRates = new List<ExchangeRate>();
                List<string> symbols = new();
                symbols.AddRange(localEditableDataItem.Select(x => x.CurrencyIn).Distinct());
                symbols.AddRange(localEditableDataItem.Select(x => x.CurrencyOut).Distinct());
                symbols.Add("aud");
                symbols.Add("usd");
                // List<int> years = new();
                foreach (var symbol in symbols.Distinct())
                {
                    if (!string.IsNullOrWhiteSpace(symbol))
                    {
                        List<int> years = new()
                        {
                            localEditableDataItem?.Where(x => x.CurrencyIn == symbol).MinBy(x => x.TransactionDate)?.TransactionDate.Year ?? 0,
                            localEditableDataItem?.Where(x => x.CurrencyOut == symbol).MinBy(x => x.TransactionDate)?.TransactionDate.Year ?? 0,
                            localEditableDataItem?.Where(x => x.CurrencyIn == symbol).MaxBy(x => x.TransactionDate)?.TransactionDate.Year ?? 0,
                            localEditableDataItem?.Where(x => x.CurrencyOut == symbol).MaxBy(x => x.TransactionDate)?.TransactionDate.Year ?? 0
                        };

                        //remove all items from years where years is 0
                        years.RemoveAll(x => x == 0);
                        if (years.Count > 0)
                        {
                            var startYear = years.Min();
                            var endYear = years.Max();
                            var response = await LoadCryptoExchangeRates(symbol.ToLower(), startYear, endYear);
                            if (response is not null && response.Count > 0)
                            {
                                allExchangeRates.AddRange(response);
                            }
                        }
                    }
                }

                foreach (var record in localEditableDataItem.OrderBy(x=>x.Sequence))
                {
                    try
                    {
                        //_progressMessage = $"processing seq: {record.Sequence.ToString()}";

                        var flattenRecords = Utils.Taxify.Flatten(record);
                        var nonNFTExchangeRateEnrichedRecords = Utils.Taxify.AddExchangeRates(flattenRecords, allExchangeRates);
                        var NFTExchangeRateEnrichedRecords = Utils.Taxify.AddNFTExchangeRates(flattenRecords, allExchangeRates);
                        if (nonNFTExchangeRateEnrichedRecords is not null && nonNFTExchangeRateEnrichedRecords.Count > 0)
                        {
                            localEditableTaxDataItem.AddRange(nonNFTExchangeRateEnrichedRecords);
                        }

                        if (NFTExchangeRateEnrichedRecords is not null && NFTExchangeRateEnrichedRecords.Count > 0)
                        {
                            localEditableTaxDataItem.AddRange(NFTExchangeRateEnrichedRecords);
                        }

                     



                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                portofolios = new();
                foreach (var item in localEditableTaxDataItem)
                {
                    //we are going to calculate the complete portofolio worth for each token
                    try
                    {
                        bool itemExists = portofolios.Any(x => x.Token.ToLower() == item.AmountAssetType.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency.ToLower());
                        if (item.TransactionType.ToUpper() == "BUY")
                        {

                            if (itemExists)
                            {
                                var portofolio = portofolios.FirstOrDefault(x => x.Token.ToLower() == item.AmountAssetType.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency.ToLower());
                                portofolio.Amount += item.Amount ?? 0;
                                portofolio.InvestmentValue += item.Value ?? 0;
                                portofolio.LastUpdated = DateTime.UtcNow;

                            }
                            else
                            {
                                Portofolio portofolio = new();
                                portofolio.Token = item.AmountAssetType.ToLower();
                                portofolio.Amount = item.Amount ?? 0;
                                portofolio.InvestmentValue = item.Value ?? 0;
                                portofolio.InvestmentValueCurrency = item.ExchangeRateCurrency;
                                portofolio.LastUpdated = DateTime.UtcNow;
                                portofolios.Add(portofolio);
                            }
                        }

                        if (item.TransactionType.ToUpper() == "SELL")
                        {

                            if (itemExists)
                            {
                                var portofolio = portofolios.FirstOrDefault(x => x.Token.ToLower() == item.AmountAssetType.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency.ToLower());
                                portofolio.Amount -= item.Amount ?? 0;
                                portofolio.InvestmentValue -= item.Value ?? 0;
                                portofolio.LastUpdated = DateTime.UtcNow;

                            }
                            else
                            {
                                Portofolio portofolio = new();
                                portofolio.Token = item.AmountAssetType.ToLower();
                                portofolio.Amount = (item.Amount ?? 0) * -1;
                                portofolio.InvestmentValue = (item.Value ?? 0) * -1;
                                portofolio.InvestmentValueCurrency = item.ExchangeRateCurrency;
                                portofolio.LastUpdated = DateTime.UtcNow;
                                portofolios.Add(portofolio);
                            }
                        }

                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                }

                if (localEditableTaxDataItem.Count > 0)
                {
                    inTaxMode = true;
                    if (taxRecordGrid is null)
                    {
                        taxRecordGrid = new();
                        await InvokeAsync(StateHasChanged);
                    }

                    await taxRecordGrid.RefreshDataAsync();
                }

                _showLoader = false;
                await InvokeAsync(StateHasChanged);
            }
        }


        private async Task<List<ExchangeRate>> LoadCryptoExchangeRates(string symbol, int startYear = 2015, int endYear = 2022)
        {
            var currentIterationYear = startYear;
            List<ExchangeRate> cryptoExchangeRateCollection = new List<ExchangeRate>();
            while (currentIterationYear <= endYear)
            {
                try
                {
                    var cryptoExchangeRates = await _HttpCLient.GetFromJsonAsync<List<ExchangeRate>>($"/config/{currentIterationYear}/{symbol}.json");
                    if (cryptoExchangeRates is not null && cryptoExchangeRates.Count > 0)
                    {
                        cryptoExchangeRateCollection.AddRange(cryptoExchangeRates);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error loading file /config/{currentIterationYear}/{symbol}.json -> {ex.Message}");
                }
                finally
                {
                    currentIterationYear++;
                }
            }

            return cryptoExchangeRateCollection;
        }

        private async Task ExportRecords(List<CryptoTaxRecords> records)
        {
            var engine = new FileHelperEngine<CryptoTaxRecords>();
            engine.HeaderText = engine.GetFileHeader();
            var outputString = engine.WriteString(records); // flattenedRecords);
            using (var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(outputString)))
            {
                using (var streamRef = new DotNetStreamReference(stream: outputStream))
                {
                    //download using JS
                    string fName = string.Concat(DateTime.Now.ToString("yyyy-MM-dd"), "_", DateTime.Now.Ticks, ".csv");
                    await JS.InvokeVoidAsync("downloadFile", fName, streamRef);
                }
            }
        }

        private void SetCustomLabelNames()
        {
            switch (selectedTradeAction)
            {
                case TradeAction.sell:
                    {
                        InName = "Amount (sold)";
                        InCurrency = "Currency";
                        OutName = "Amount (bought)";
                        OutCurrency = "Currency";
                        break;
                    }

                case TradeAction.buy:
                    {
                        InName = "Amount (bought)";
                        InCurrency = "Currency";
                        OutName = string.Empty;
                        OutCurrency = string.Empty;
                        break;
                    }

                case TradeAction.nftsell:
                    {
                        InName = "NFT (quantity)";
                        InCurrency = "Name";
                        OutName = "Amount (worth)";
                        OutCurrency = "Currency";
                        break;
                    }

                case TradeAction.nftbuy:
                    {
                        InName = "Amount (bought)";
                        InCurrency = "Currency";
                        OutName = "NFT (quantity)";
                        OutCurrency = "Name";
                        break;
                    }

                case TradeAction.stake:
                    {
                        InName = "Amount (staked)";
                        InCurrency = "Currency";
                        OutName = string.Empty;
                        OutCurrency = string.Empty;
                        break;
                    }

                case TradeAction.unstake:
                    {
                        InName = string.Empty;
                        InCurrency = string.Empty;
                        OutName = "Amount (unstaked)";
                        OutCurrency = "Currency";
                        break;
                    }

                case TradeAction.transfer:
                    {
                        InName = "Transfer out (quantity)";
                        InCurrency = "Currency";
                        OutName = "Transfer in (quantity)";
                        OutCurrency = "Currency";
                        break;
                    }

                default:
                    {
                        InName = "Amount (in)";
                        InCurrency = "Currency (in)";
                        OutName = "Amount (out)";
                        OutCurrency = "Currency (out)";
                        break;
                    }
            }
        }

        private async Task InitializeCryptoCollection(int endYear)
        {
            if (endYear == 0)
            {
                var maxTransactionDate = localEditableTaxDataItem.Max(x => x.TransactionDate);
                if (maxTransactionDate.Month > 6)
                {
                    endYear = maxTransactionDate.Year + 1;
                }
                else
                {
                    endYear = maxTransactionDate.Year;
                }
            }

            Console.WriteLine($"tax year: {endYear - 1}/{endYear}");
            _showLoader = true;
            await InvokeAsync(StateHasChanged);
            myCryptoCollection = CryptoCollectionFactory.CreateCryptoCollection(localEditableTaxDataItem);
            if(TaxRecords is not null && TaxRecords.Count > 0)
            {
                TaxRecords.Clear();
            }
            //var consolidatedView = Utils.Taxify.DailyConsolidation(localEditableTaxDataItem);
            //Console.WriteLine($"consolidated count: {consolidatedView.Count()} vs original count: {localEditableDataItem.Count()}");
            TaxRecords.AddRange(Utils.Taxify.CreateCryptoTaxRecords(myCryptoCollection, localEditableTaxDataItem, allExchangeRates, endYear));
            if (TaxRecords.Count > 0)
            {

                var taxResultString = TaxRecords.Select(x =>
                {
                    return $"on {x.SellDate} sold {x.SellAmount} of {x.Name}, bought at {x.BuyPrice} and sold for {x.SellPrice}, capital gains result {x.CapitalGainAmount}";
                }).ToList();
                if (taxResultString is not null && taxResultString.Count > 0)
                {
                    TaxTransactions.AddRange(taxResultString);
                }

                _showLoader = false;
            }

            WalletCollection.AddRange(Utils.Taxify.CalculateWalletValues(myCryptoCollection, allExchangeRates));
            await InvokeAsync(StateHasChanged);
        }
    }
}