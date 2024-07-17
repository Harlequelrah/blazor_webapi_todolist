using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace test.Services
{
    public class TodoItemService
    {
        private readonly HttpClient _authClient;
        private readonly HttpClient _noauthClient;
        private readonly CustomAuthenticationStateProvider _customAuthenticationStateProvider;

        private readonly ILogger<TodoItemService> _logger;

        public TodoItemService(IHttpClientFactory httpClientFactory, ILogger<TodoItemService> logger, CustomAuthenticationStateProvider customAuthenticationStateProvider)
        {
            _authClient = httpClientFactory.CreateClient("authClientAPI");
            _noauthClient = httpClientFactory.CreateClient("noauthClientAPI");
            _logger = logger;
            _customAuthenticationStateProvider = customAuthenticationStateProvider;
        }

        public async Task<List<TodoItem>> GetTodoItemsAsync()
        {
            try
            {
                var todoItemsArray = await _noauthClient.GetFromJsonAsync<TodoItem[]>("todo");
                return new List<TodoItem>(todoItemsArray);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving todo items: {ex.Message}");
                return new List<TodoItem>(); // Retourne une liste vide en cas d'erreur
            }
        }

        public async Task<TodoItem> GetTodoItemAsync(int id)
        {
            try
            {
                return await _noauthClient.GetFromJsonAsync<TodoItem>($"todo/{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving todo item {id}: {ex.Message}");
                return null; // Retourne null en cas d'erreur
            }
        }

        public async Task<TodoItem> CreateTodoItemAsync(TodoItem todoItem)
        {
            try
            {
                var response = await _noauthClient.PostAsJsonAsync("todo", todoItem);
                return await response.Content.ReadFromJsonAsync<TodoItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating todo item: {ex.Message}");
                return null; // Retourne null en cas d'erreur
            }
        }

        public async Task UpdateTodoItemAsync(int id, TodoItem todoItem)
        {
            try
            {
                await _authClient.PutAsJsonAsync($"todo/{id}", todoItem);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating todo item {id}: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task SetTodoItemDoneAsync(int id)
        {
            try
            {
                var todoItem = await GetTodoItemAsync(id);
                if (todoItem != null)
                {
                    todoItem.IsCompleted = true;
                    await UpdateTodoItemAsync(id, todoItem);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting todo item {id} as done: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task SetTodoItemNotDoneAsync(int id)
        {
            try
            {
                var todoItem = await GetTodoItemAsync(id);
                if (todoItem != null)
                {
                    todoItem.IsCompleted = false;
                    await UpdateTodoItemAsync(id, todoItem);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting todo item {id} as not done: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task<bool> DeleteTodoItemAsync(int id)
        {
            try
            {
                var isprerendering = await _customAuthenticationStateProvider.GetRendering();
                Console.WriteLine($" rendering in the game {isprerendering}");
                var response = await _authClient.DeleteAsync($"todo/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting todo item {id}: {ex.Message}");
                return false; // Retourne false en cas d'erreur
            }
        }
    }
    public class TodoItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool IsCompleted { get; set; }
    }
}
