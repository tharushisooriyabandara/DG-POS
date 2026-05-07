using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class CardMachineUserService
    {
        private static CardMachineUserService _instance;
        public static CardMachineUserService Instance => _instance ??= new CardMachineUserService();

        private readonly string _filePath;
        private Dictionary<string, ObservableCollection<CardMachineUserModel>> _usersByTerminal;

        private CardMachineUserService()
        {
            PathService.EnsureInitialized();
            _filePath = PathService.GetFilePath("cardmachine_users.txt");
            _usersByTerminal = new Dictionary<string, ObservableCollection<CardMachineUserModel>>();
            LoadUsers();
        }

        public ObservableCollection<CardMachineUserModel> GetUsersForTerminal(string terminalId)
        {
            if (!_usersByTerminal.ContainsKey(terminalId))
            {
                _usersByTerminal[terminalId] = new ObservableCollection<CardMachineUserModel>();
            }
            return _usersByTerminal[terminalId];
        }

        public void AddUser(string terminalId, CardMachineUserModel user)
        {
            if (user == null) return;

            user.Tid = terminalId;
            var users = GetUsersForTerminal(terminalId);
            users.Add(user);
            SaveUsers();
        }

        public void DeleteUser(string terminalId, CardMachineUserModel user)
        {
            if (user == null) return;

            var users = GetUsersForTerminal(terminalId);
            users.Remove(user);
            SaveUsers();
        }

        private void LoadUsers()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string jsonContent = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        _usersByTerminal = JsonConvert.DeserializeObject<Dictionary<string, ObservableCollection<CardMachineUserModel>>>(jsonContent);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading card machine users: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SaveUsers()
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(_usersByTerminal, Formatting.Indented);
                File.WriteAllText(_filePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving card machine users: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void Refresh()
        {
            LoadUsers();
        }
    }
} 