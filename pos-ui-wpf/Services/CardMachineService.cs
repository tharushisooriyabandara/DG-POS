using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class CardMachineService
    {
        private static CardMachineService _instance;
        public static CardMachineService Instance => _instance ??= new CardMachineService();

        private readonly string _filePath;
        private ObservableCollection<CardMachineModel> _cardMachines;

        private CardMachineService()
        {
            // Create file path in centralized ALLPOSDETAILS folder
            PathService.EnsureInitialized();
            _filePath = PathService.GetFilePath("cardmachine.txt");
            _cardMachines = new ObservableCollection<CardMachineModel>();
            LoadCardMachines();
        }

        public ObservableCollection<CardMachineModel> CardMachines
        {
            get => _cardMachines;
            set
            {
                _cardMachines = value;
                SaveCardMachines();
            }
        }

        public void AddCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            _cardMachines.Add(cardMachine);
            SaveCardMachines();
        }

        public void UpdateCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            var existingMachine = _cardMachines.FirstOrDefault(c => c.DeviceId == cardMachine.DeviceId);
            if (existingMachine != null)
            {
                var index = _cardMachines.IndexOf(existingMachine);
                _cardMachines[index] = cardMachine;
                SaveCardMachines();
            }
        }

        public void DeleteCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            _cardMachines.Remove(cardMachine);
            SaveCardMachines();
        }

        public void ToggleCardMachineStatus(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;

            cardMachine.IsActive = !cardMachine.IsActive;
            SaveCardMachines();
        }

        private void LoadCardMachines()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string jsonContent = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var cardMachinesList = JsonConvert.DeserializeObject<List<CardMachineModel>>(jsonContent);
                        _cardMachines.Clear();
                        foreach (var machine in cardMachinesList)
                        {
                            _cardMachines.Add(machine);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading card machines: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SaveCardMachines()
        {
            try
            {
                var cardMachinesList = _cardMachines.ToList();
                string jsonContent = JsonConvert.SerializeObject(cardMachinesList, Formatting.Indented);
                File.WriteAllText(_filePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving card machines: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void Refresh()
        {
            LoadCardMachines();
        }
    }
} 