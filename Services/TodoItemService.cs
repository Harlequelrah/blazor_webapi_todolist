using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace test.Services
{
    public class TodoItemService
    {
        private readonly HttpClient _authClient;
        private readonly HttpClient _noauthClient;
        private readonly ILogger<TodoItemService> _logger;

        public TodoItemService(IHttpClientFactory httpClientFactory, ILogger<TodoItemService> logger)
        {
            _authClient = httpClientFactory.CreateClient("authClientAPI") ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _noauthClient = httpClientFactory.CreateClient("noauthClientAPI") ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger;
            _logger = logger;
        }

        public async Task<List<TodoItem>> GetTodoItemsAsync()
        {
            try
            {
                _logger.LogInformation("Getting todo items.");
                var todoItemsArray = await _noauthClient.GetFromJsonAsync<TodoItem[]>("todo");
                _logger.LogInformation("Successfully retrieved todo items.");
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
                _logger.LogInformation($"Getting todo item with ID {id}.");
                var todoItem = await _noauthClient.GetFromJsonAsync<TodoItem>($"todo/{id}");
                _logger.LogInformation($"Successfully retrieved todo item with ID {id}.");
                return todoItem;
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
                _logger.LogInformation($"Creating todo item: {todoItem.Title}.");
                var response = await _noauthClient.PostAsJsonAsync("todo", todoItem);
                var createdItem = await response.Content.ReadFromJsonAsync<TodoItem>();
                _logger.LogInformation($"Successfully created todo item with ID {createdItem.Id}.");
                return createdItem;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating todo item: {ex.Message}");
                return null; // Retourne null en cas d'erreur
            }
        }

        public async Task UpdateTodoItemAsync(string token, int id, TodoItem todoItem)
        {
            try
            {
                _logger.LogInformation($"Updating todo item with ID {id}.");
                var jsonData = JsonSerializer.Serialize(todoItem);
                var httpContent = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"todo/{id}")
                {
                    Content = httpContent
                };
                var response = await _authClient.AuthSendAsync(requestMessage, token, CancellationToken.None);
                _logger.LogInformation($"Successfully updated todo item with ID {id}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating todo item {id}: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task SetTodoItemDoneAsync(string token, int id)
        {
            try
            {
                _logger.LogInformation($"Setting todo item with ID {id} as done.");
                var todoItem = await GetTodoItemAsync(id);
                if (todoItem != null)
                {
                    todoItem.IsCompleted = true;
                    await UpdateTodoItemAsync(token, id, todoItem);
                    _logger.LogInformation($"Successfully set todo item with ID {id} as done.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting todo item {id} as done: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task SetTodoItemNotDoneAsync(string token, int id)
        {
            try
            {
                _logger.LogInformation($"Setting todo item with ID {id} as not done.");
                var todoItem = await GetTodoItemAsync(id);
                if (todoItem != null)
                {
                    todoItem.IsCompleted = false;
                    await UpdateTodoItemAsync(token, id, todoItem);
                    _logger.LogInformation($"Successfully set todo item with ID {id} as not done.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting todo item {id} as not done: {ex.Message}");
                // Gérer l'erreur ici si nécessaire
            }
        }

        public async Task<bool> DeleteTodoItemAsync(string token, int id)
        {
            try
            {
                _logger.LogInformation($"Deleting todo item with ID {id}.");
                var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"todo/{id}");
                var response = await _authClient.AuthSendAsync(requestMessage, token, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully deleted todo item with ID {id}.");
                    return true;
                }
                else
                {
                    _logger.LogError($"Failed to delete todo item with ID {id}. Status code: {response.StatusCode}");
                    return false;
                }
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
