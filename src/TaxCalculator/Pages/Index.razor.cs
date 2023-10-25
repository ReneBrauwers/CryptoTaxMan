using Common.Models;
using Common.Extensions;
using FileHelpers;
using global::Microsoft.AspNetCore.Components;
using Havit.Blazor.Components.Web.Bootstrap;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text;
using TaxCalculator.Models;
using static TaxCalculator.Utils.Enums;
using TaxCalculator.Services;

namespace TaxCalculator.Pages
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
        private List<Portofolio> negativePortofolios;

        private string _uploadFileName = string.Empty;
        private MarkupString messageArea;

        [Inject] 
        IJSRuntime JS { get;set; }

        [Inject]
        CryptoTaxManDbContext _dbContext { get; set; }
        protected override void OnInitialized()
        {


            int startYear = 2015;
            while (startYear <= DateTime.UtcNow.Year)
            {
                TaxYears.Add(startYear);
                startYear++;
            }
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
                 
                        }

                        _uploadFileName = e.File.Name;
                    }
                }
            }

            //_localStorageInUse = await _LocalStorage.ContainKeyAsync(_uploadFileName);
            //await ExportRecords(_importedRecords);
            //_showLoader = false;
            //StateHasChanged();
        }

        private async void UploadFileAndValidate(InputFileChangeEventArgs e)
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

                            //perform a validation before proceeding
                            List<CryptoTransactionRecord> recordsToValidate = new List<CryptoTransactionRecord>();
                         
                            localEditableDataItem.ForEach(x =>
                            {
                                recordsToValidate.AddRange(Utils.Taxify.Flatten(x));
                            });
                            
                            Utils.Taxify.ValidateCryptoTransactionRecords(recordsToValidate);
                            if(recordsToValidate.Any(x=>x.FlaggedForReview))
                            {
                                 var flaggedItem = recordsToValidate.Where(x => x.FlaggedForReview).First();
                                 messageArea = new MarkupString($"There is an issue with the record on line {flaggedItem.Sequence} <p> {flaggedItem.InternalNotes}</p>.<br>Please review and resubmit");
                                StateHasChanged();
                                return;
                            }

                            await recordsToImportGrid.RefreshDataAsync();

                        }

                        _uploadFileName = e.File.Name;
                    }
                }
            }

            //_localStorageInUse = await _LocalStorage.ContainKeyAsync(_uploadFileName);
            //await ExportRecords(_importedRecords);
            //_showLoader = false;
            //StateHasChanged();
        }

        private async Task CreateTaxRecords()
        {
            _showLoader = true;
            await InvokeAsync(StateHasChanged);
            if (localEditableDataItem.Count > 0)
            {
                foreach (var item in localEditableDataItem)
                {
                    var response = await LookupExchangeRate(item);
                    if (response is not null && response.Count > 0)
                    {
                        allExchangeRates.AddRange(response);
                    }
                }
            }

            foreach (var record in localEditableDataItem.OrderBy(x => x.Sequence))
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
                    //
                    Console.WriteLine(ex.Message);
                }
            }

            //perform a validation before proceeding
            Utils.Taxify.ValidateCryptoTransactionRecords(localEditableTaxDataItem);

            if(localEditableTaxDataItem.Any(x=>x.FlaggedForReview))
            {

                inTaxMode = true;
                if (taxRecordGrid is null)
                {
                    taxRecordGrid = new();
                   
                }
                await InvokeAsync(StateHasChanged);
                return;
            }

            portofolios = new();
            negativePortofolios = new();

            foreach (var item in localEditableTaxDataItem)
            {
                //we are going to calculate the complete portofolio worth for each token
                try
                {
                    bool itemExists = portofolios.Any(x => x.Token.ToLower() == item.AmountAssetType?.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency?.ToLower());
                    if (item?.TransactionType?.ToUpper() == "BUY")
                    {

                        if (itemExists)
                        {
                            var portofolio = portofolios.FirstOrDefault(x => x.Token.ToLower() == item.AmountAssetType?.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency?.ToLower());
                            portofolio.Amount += item.Amount ?? 0;
                            portofolio.InvestmentValue += item.Value ?? 0;
                            portofolio.LastUpdated = DateTime.UtcNow;

                        }
                        else
                        {
                            Portofolio portofolio = new();
                            portofolio.Token = item.AmountAssetType?.ToLower() ?? string.Empty;
                            portofolio.Amount = item.Amount ?? 0;
                            portofolio.InvestmentValue = item.Value ?? 0;
                            portofolio.InvestmentValueCurrency = item?.ExchangeRateCurrency ?? string.Empty;
                            portofolio.LastUpdated = DateTime.UtcNow;
                            portofolios.Add(portofolio);
                        }
                    }


                    if (item?.TransactionType?.ToUpper() == "SELL")
                    {
                        double amountToDeduct = item.Amount ?? 0;
                        double valueToDeduct = item.Value ?? 0;

                        while (amountToDeduct > 0 && itemExists)
                        {
                            var portofolio = portofolios.FirstOrDefault(x => x.Token.ToLower() == item.AmountAssetType?.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency?.ToLower());

                            if (portofolio.Amount >= amountToDeduct)
                            {
                                portofolio.Amount -= amountToDeduct;
                                portofolio.InvestmentValue -= valueToDeduct;
                                portofolio.LastUpdated = DateTime.UtcNow;
                                break;
                            }
                            else
                            {
                                amountToDeduct -= portofolio.Amount;
                                valueToDeduct -= portofolio.InvestmentValue;
                                portofolio.Amount = 0;
                                portofolio.InvestmentValue = 0;
                                portofolio.LastUpdated = DateTime.UtcNow;

                                // Check if there's another matching portfolio item
                                itemExists = portofolios.Any(x => x.Token.ToLower() == item.AmountAssetType?.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency?.ToLower() && x.Amount > 0);
                            }
                        }

                        if (amountToDeduct > 0 && !itemExists)
                        {
                            // Create a negative balance portfolio
                            Portofolio negativeBalancePortofolio = new();
                            negativeBalancePortofolio.Token = item.AmountAssetType?.ToLower() ?? string.Empty;
                            negativeBalancePortofolio.Amount = -amountToDeduct;
                            negativeBalancePortofolio.InvestmentValue = -valueToDeduct;
                            negativeBalancePortofolio.InvestmentValueCurrency = item?.ExchangeRateCurrency ?? string.Empty;
                            negativeBalancePortofolio.LastUpdated = DateTime.UtcNow;
                            negativePortofolios.Add(negativeBalancePortofolio);
                        }


                        //if (item?.TransactionType?.ToUpper() == "SELL")
                        //{

                        //    if (itemExists)
                        //    {
                        //        var portofolio = portofolios.FirstOrDefault(x => x.Token.ToLower() == item.AmountAssetType?.ToLower() && x.InvestmentValueCurrency.ToLower() == item.ExchangeRateCurrency?.ToLower());
                        //        portofolio.Amount -= item.Amount ?? 0;
                        //        portofolio.InvestmentValue -= item.Value ?? 0;
                        //        portofolio.LastUpdated = DateTime.UtcNow;

                        //    }
                        //    else
                        //    {
                        //        Portofolio portofolio = new();
                        //        portofolio.Token = item.AmountAssetType?.ToLower() ?? string.Empty;
                        //        portofolio.Amount = (item.Amount ?? 0) * -1;
                        //        portofolio.InvestmentValue = (item.Value ?? 0) * -1;
                        //        portofolio.InvestmentValueCurrency = item?.ExchangeRateCurrency ?? string.Empty;
                        //        portofolio.LastUpdated = DateTime.UtcNow;
                        //        portofolios.Add(portofolio);
                        //    }
                        //}
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

                //store in localstorage
                //if (!_localStorageInUse)
                //{
                //    await _LocalStorage.SetItemAsync(_uploadFileName, localEditableTaxDataItem);
                //}

                await taxRecordGrid.RefreshDataAsync();
            }

            _showLoader = false;
            await InvokeAsync(StateHasChanged);

        }
   
        private async Task<List<ExchangeRate>> LookupExchangeRate(CryptoTransactionRecordImport lookup)
        {
            if (lookup is null)
            {
                return new List<ExchangeRate>();
            }

            List<ExchangeRate> cryptoExchangeRates = new List<ExchangeRate>();
            
            List<(DateTime TransactionDate, string CurrencyIn, string ExchangeCurrency)> searchKeys = new List<(DateTime TransactionDate,string CurrencyIn, string ExchangeCurrency)>();
            //using (var dbContext = await _dbContextFactory.CreateDbContextAsync())
            //{

                //construct pk lookup


                if (!string.IsNullOrWhiteSpace(lookup.CurrencyIn))
                {
                    searchKeys.Add((lookup.TransactionDate.FromSpecifiedToUTC("Australia/Sydney").Date,lookup.CurrencyIn, "aud"));
                }

                if (!string.IsNullOrWhiteSpace(lookup.CurrencyOut))
                {
                    searchKeys.Add((lookup.TransactionDate.FromSpecifiedToUTC("Australia/Sydney").Date,lookup.CurrencyOut, "aud"));
                }

                if (!string.IsNullOrEmpty(lookup.FeeCurrency))
                {
                    searchKeys.Add((lookup.TransactionDate.FromSpecifiedToUTC("Australia/Sydney").Date,lookup.FeeCurrency, "aud"));
                }



                //get all matching exchange rates 
                foreach (var key in searchKeys)
                {
                try
                {
                   
                    var compositeKey = new object[] {  key.TransactionDate, key.CurrencyIn, key.ExchangeCurrency };
                    var result = await _dbContext.ExchangeRates.FindAsync(compositeKey);
                    if (result is not null)
                    {
                        cryptoExchangeRates.Add(result);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"{key.TransactionDate}, {key.CurrencyIn},{key.ExchangeCurrency} caused error {ex.Message}");
                }

                }


            

            return cryptoExchangeRates;
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
                if(selectedTaxDataItem.FlaggedForReview)
                {
                    //there should only be one flagged for review at a time
                    var flaggedForReview = localEditableTaxDataItem.Where(x => x.FlaggedForReview).First();
                    if(flaggedForReview is not null)
                    {
                        flaggedForReview.FlaggedForReview = false;
                    }                    
                    

                    Utils.Taxify.ValidateCryptoTransactionRecords(localEditableTaxDataItem);
                    if (localEditableTaxDataItem.Any(x => x.FlaggedForReview))
                    {

                        inTaxMode = true;
                        if (taxRecordGrid is null)
                        {
                            taxRecordGrid = new();

                        }
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                }
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



        //private async Task ClearLocalStorage()
        //{
        //    await _LocalStorage.ClearAsync();
        //    _localStorageInUse = false;
        //    StateHasChanged();
        //}




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


        private async Task ExportCryptoTransactionRecord()
        {
            if (localEditableTaxDataItem is not null)
            {
                List<CryptoTransactionRecord> exportTransactions = new();
                exportTransactions.AddRange(localEditableTaxDataItem);
                exportTransactions.ForEach(x =>

                {
                    if (x.TransactionType.ToUpper() == "SELL")
                    {
                        x.Amount = x.Amount * -1;
                    }
                });

                var engine = new FileHelperEngine<CryptoTransactionRecord>();
                engine.HeaderText = engine.GetFileHeader();
                var outputString = engine.WriteString(exportTransactions); // flattenedRecords);
                using (var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(outputString)))
                {
                    using (var streamRef = new DotNetStreamReference(stream: outputStream))
                    {
                        //download using JS
                        string fName = string.Concat("CryptoTransactionRecordsExport_", DateTime.Now.Ticks, ".csv");
                        await JS.InvokeVoidAsync("downloadFile", fName, streamRef);
                    }
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
            if (TaxRecords is not null && TaxRecords.Count > 0)
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