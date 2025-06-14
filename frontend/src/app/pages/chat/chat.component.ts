import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import { ApiService } from '../../services/api.service';

interface ChatMessage {
  id: string;
  content: string;
  role: 'user' | 'assistant';
  timestamp: Date;
  htmlContent?: SafeHtml;
}

interface ChatRequest {
  message: string;
  history: ChatMessage[];
  systemPrompt?: string;
  context?: string;
  settings?: {
    temperature: number;
    maxTokens: number;
    model: string;
  };
}

interface ChatResponse {
  message: string;
  id: string;
}

interface InitializeChatRequest {
  context: string;
  file?: File;
  systemPrompt: string;
  firstMessage: string;
  settings: {
    temperature: number;
    maxTokens: number;
    model: string;
  };
}

interface InitializeChatResponse {
  conversationId: string;
  message: string;
  messageId: string;
  timestamp: string;
}

interface ChatMessageRequest {
  conversationId: string;
  userMessage: string;
  settings?: {
    temperature: number;
    maxTokens: number;
    model: string;
  };
}

interface ConversationListItem {
  id: string;
  createdAt: string;
  expiryAt?: string;
  messageCount: number;
  documentName?: string;
  isActive: boolean;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent {
  private http = inject(HttpClient);
  private sanitizer = inject(DomSanitizer);
  private apiService = inject(ApiService);
  
  systemPrompt = signal<string>('You are a helpful AI assistant.');
  contextInfo = signal<string>('');
  messages = signal<ChatMessage[]>([]);
  currentMessage = signal<string>('');
  isStreaming = signal<boolean>(false);
  useCaching = signal<boolean>(false);
  selectedFile = signal<File | null>(null);
  currentConversationId = signal<string | null>(null);
  conversations = signal<ConversationListItem[]>([]);
  isInitialized = signal<boolean>(false);
  
  readonly MAX_SYSTEM_PROMPT_LENGTH = 2000;
  readonly MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
  readonly ALLOWED_FILE_TYPES = ['application/pdf', 'text/plain', 'text/markdown'];

  constructor() {
    // Configure marked options
    marked.setOptions({
      breaks: true,
      gfm: true
    });
  }

  getCurrentTime() {
    return new Date();
  }

  get systemPromptLength() {
    return this.systemPrompt().length;
  }

  get isSystemPromptValid() {
    return this.systemPromptLength <= this.MAX_SYSTEM_PROMPT_LENGTH;
  }

  get canStartChat() {
    if (!this.useCaching()) {
      return !this.isStreaming();
    }
    
    // For caching mode, need context or file
    const hasContext = this.contextInfo().trim().length > 0;
    const hasFile = this.selectedFile() !== null && this.isFileValid;
    return !this.isStreaming() && (hasContext || hasFile);
  }

  get fileSize() {
    const file = this.selectedFile();
    return file ? `${(file.size / 1024 / 1024).toFixed(2)} MB` : '';
  }

  get isFileValid() {
    const file = this.selectedFile();
    if (!file) return true;
    
    return file.size <= this.MAX_FILE_SIZE && 
           this.ALLOWED_FILE_TYPES.includes(file.type);
  }

  addMessage(content: string, role: 'user' | 'assistant') {
    const newMessage: ChatMessage = {
      id: crypto.randomUUID(),
      content,
      role,
      timestamp: new Date()
    };

    // Convert markdown to HTML for assistant messages
    if (role === 'assistant') {
      const htmlContent = marked(content) as string;
      newMessage.htmlContent = this.sanitizer.bypassSecurityTrustHtml(htmlContent);
    }
    
    this.messages.update(messages => [...messages, newMessage]);
    return newMessage;
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    
    if (file) {
      if (file.size > this.MAX_FILE_SIZE) {
        alert(`File size must be less than ${this.MAX_FILE_SIZE / 1024 / 1024}MB`);
        input.value = '';
        return;
      }
      
      if (!this.ALLOWED_FILE_TYPES.includes(file.type)) {
        alert(`File type not supported. Allowed types: PDF, TXT, MD`);
        input.value = '';
        return;
      }
      
      this.selectedFile.set(file);
    } else {
      this.selectedFile.set(null);
    }
  }

  removeFile() {
    this.selectedFile.set(null);
    // Reset file input
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.value = '';
    }
  }

  async toggleCaching() {
    const newCachingState = !this.useCaching();
    this.useCaching.set(newCachingState);
    
    if (!newCachingState) {
      // Switched to non-caching mode
      this.currentConversationId.set(null);
      this.isInitialized.set(false);
      this.selectedFile.set(null);
      this.removeFile();
    } else {
      // Switched to caching mode - load conversations
      await this.loadConversations();
    }
  }

  async loadConversations() {
    try {
      const conversations = await this.http.get<ConversationListItem[]>(this.apiService.getApiUrl('/chat/conversations')).toPromise();
      this.conversations.set(conversations || []);
    } catch (error) {
      console.error('Error loading conversations:', error);
    }
  }

  async selectConversation(conversationId: string) {
    this.currentConversationId.set(conversationId);
    this.isInitialized.set(true);
    // Clear current messages and load conversation history if needed
    this.messages.set([]);
  }

  async deleteConversation(conversationId: string) {
    try {
      await this.http.delete(this.apiService.getApiUrl(`/chat/conversation/${conversationId}`)).toPromise();
      await this.loadConversations();
      
      if (this.currentConversationId() === conversationId) {
        this.currentConversationId.set(null);
        this.isInitialized.set(false);
        this.messages.set([]);
      }
    } catch (error) {
      console.error('Error deleting conversation:', error);
    }
  }

  async sendMessage() {
    const message = this.currentMessage().trim();
    if (!message || this.isStreaming()) return;

    if (this.useCaching() && !this.isInitialized()) {
      await this.initializeChat(message);
      return;
    }

    // Add user message
    this.addMessage(message, 'user');
    this.currentMessage.set('');

    this.isStreaming.set(true);

    try {
      if (this.useCaching() && this.currentConversationId()) {
        await this.streamCachedMessage(message);
      } else {
        // Regular non-cached mode
        const request: ChatRequest = {
          message,
          history: this.messages(),
          systemPrompt: this.systemPrompt(),
          context: this.contextInfo(),
          settings: {
            temperature: 0.7,
            maxTokens: 1000,
            model: "gemini-2.0-flash"
          }
        };
        await this.streamMessage(request);
      }
    } catch (error) {
      console.error('Chat error:', error);
      this.addMessage('Sorry, there was an error processing your message.', 'assistant');
    } finally {
      this.isStreaming.set(false);
    }
  }

  async initializeChat(firstMessage: string) {
    this.isStreaming.set(true);

    try {
      const formData = new FormData();
      
      // Ensure context is provided - use placeholder if only file is uploaded
      const contextValue = this.contextInfo().trim() || 'Document uploaded for context';
      formData.append('context', contextValue);
      formData.append('systemPrompt', this.systemPrompt());
      formData.append('firstMessage', firstMessage);
      formData.append('settings.temperature', '0.7');
      formData.append('settings.maxTokens', '1000');
      formData.append('settings.model', 'gemini-2.0-flash');

      const file = this.selectedFile();
      if (file) {
        formData.append('file', file);
      }

      // Add user message
      this.addMessage(firstMessage, 'user');
      this.currentMessage.set('');

      const response = await this.http.post<InitializeChatResponse>(
        this.apiService.getApiUrl('/chat/initialize'),
        formData
      ).toPromise();

      if (response) {
        this.currentConversationId.set(response.conversationId);
        this.isInitialized.set(true);
        
        // Add assistant response
        this.addMessage(response.message, 'assistant');
        
        // Reload conversations list
        await this.loadConversations();
      }
    } catch (error) {
      console.error('Chat initialization error:', error);
      this.addMessage('Sorry, there was an error initializing the chat.', 'assistant');
    } finally {
      this.isStreaming.set(false);
    }
  }

  async streamCachedMessage(message: string) {
    const conversationId = this.currentConversationId();
    if (!conversationId) return;

    const request: ChatMessageRequest = {
      conversationId,
      userMessage: message,
      settings: {
        temperature: 0.7,
        maxTokens: 1000,
        model: "gemini-2.0-flash"
      }
    };

    // Create a placeholder message for streaming
    const assistantMessage = this.addMessage('', 'assistant');
    let fullContent = '';

    try {
      const response = await fetch(this.apiService.getApiUrl('/chat/message/stream'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (reader) {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value);
          const lines = chunk.split('\n');

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const data = line.substring(6);
              
              if (data === '[DONE]') {
                return; // Stream completed
              }

              try {
                const parsed = JSON.parse(data);
                if (parsed.Content && !parsed.IsComplete) {
                  fullContent += parsed.Content;
                  
                  // Update both content and htmlContent in a single signal update
                  const htmlContent = marked(fullContent) as string;
                  this.messages.update(messages => 
                    messages.map(msg => 
                      msg.id === assistantMessage.id 
                        ? { 
                            ...msg, 
                            content: fullContent,
                            htmlContent: this.sanitizer.bypassSecurityTrustHtml(htmlContent)
                          }
                        : msg
                    )
                  );
                }
              } catch (e) {
                console.warn('Failed to parse streaming data:', data, e);
              }
            }
          }
        }
      }
    } catch (error) {
      console.error('Cached streaming error:', error);
      // Update the message with error content
      const errorHtml = marked('Sorry, there was an error processing your message.') as string;
      this.messages.update(messages => 
        messages.map(msg => 
          msg.id === assistantMessage.id 
            ? { 
                ...msg, 
                content: 'Sorry, there was an error processing your message.',
                htmlContent: this.sanitizer.bypassSecurityTrustHtml(errorHtml)
              }
            : msg
        )
      );
    }
  }

  private async streamMessage(request: ChatRequest) {
    // Create a placeholder message for streaming
    const assistantMessage = this.addMessage('', 'assistant');
    let fullContent = '';

    try {
      const response = await fetch(this.apiService.getApiUrl('/chat/stream'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (reader) {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value);
          const lines = chunk.split('\n');

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const data = line.substring(6);
              
              if (data === '[DONE]') {
                return; // Stream completed
              }

              try {
                const parsed = JSON.parse(data);
                if (parsed.Content && !parsed.IsComplete) {
                  fullContent += parsed.Content;
                  
                  // Update both content and htmlContent in a single signal update
                  const htmlContent = marked(fullContent) as string;
                  this.messages.update(messages => 
                    messages.map(msg => 
                      msg.id === assistantMessage.id 
                        ? { 
                            ...msg, 
                            content: fullContent,
                            htmlContent: this.sanitizer.bypassSecurityTrustHtml(htmlContent)
                          }
                        : msg
                    )
                  );
                }
              } catch (e) {
                console.warn('Failed to parse streaming data:', data, e);
              }
            }
          }
        }
      }
    } catch (error) {
      console.error('Streaming error:', error);
      // Update the message with error content
      const errorHtml = marked('Sorry, there was an error processing your message.') as string;
      this.messages.update(messages => 
        messages.map(msg => 
          msg.id === assistantMessage.id 
            ? { 
                ...msg, 
                content: 'Sorry, there was an error processing your message.',
                htmlContent: this.sanitizer.bypassSecurityTrustHtml(errorHtml)
              }
            : msg
        )
      );
    }
  }

  clearChat() {
    this.messages.set([]);
    
    if (this.useCaching()) {
      this.currentConversationId.set(null);
      this.isInitialized.set(false);
    }
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (this.canStartChat) {
        this.sendMessage();
      }
    }
  }
}