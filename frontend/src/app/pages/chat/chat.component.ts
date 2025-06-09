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
      history: this.messages(),
      systemPrompt: this.systemPrompt(),
      context: this.contextInfo(),
      settings: {
        temperature: 0.7,
        maxTokens: 1000,
        model: "gemini-2.0-flash"
      }
    };

    this.isStreaming.set(true);

    try {
      await this.streamMessage(request);
    } catch (error) {
      console.error('Chat error:', error);
      this.addMessage('Sorry, there was an error processing your message.', 'assistant');
    } finally {
      this.isStreaming.set(false);
    }
  }

  private async streamMessage(request: ChatRequest) {
    // Create a placeholder message for streaming
    const assistantMessage = this.addMessage('', 'assistant');
    let fullContent = '';

    try {
      const response = await fetch('http://localhost:5000/api/chat/stream', {
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
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }
}