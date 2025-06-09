import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';

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
}

interface ChatResponse {
  message: string;
  id: string;
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
  
  systemPrompt = signal<string>('You are a helpful AI assistant.');
  contextInfo = signal<string>('');
  messages = signal<ChatMessage[]>([]);
  currentMessage = signal<string>('');
  isStreaming = signal<boolean>(false);
  
  readonly MAX_SYSTEM_PROMPT_LENGTH = 2000;

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

  async sendMessage() {
    const message = this.currentMessage().trim();
    if (!message || this.isStreaming()) return;

    // Add user message
    this.addMessage(message, 'user');
    this.currentMessage.set('');

    // Prepare request
    const request: ChatRequest = {
      message,
      history: this.messages()
    };

    this.isStreaming.set(true);

    try {
      // For now, use the regular endpoint - we'll implement streaming later
      const response = await this.http.post<ChatResponse>('http://localhost:5000/api/chat/send', request).toPromise();
      
      if (response?.message) {
        this.addMessage(response.message, 'assistant');
      }
    } catch (error) {
      console.error('Chat error:', error);
      this.addMessage('Sorry, there was an error processing your message.', 'assistant');
    } finally {
      this.isStreaming.set(false);
    }
  }

  clearChat() {
    this.messages.set([]);
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }
}