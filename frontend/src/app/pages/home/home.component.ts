import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

interface HelloResponse {
  message: string;
  timestamp: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  private http = inject(HttpClient);
  
  backendResponse = signal<string>('');
  loading = signal<boolean>(false);
  error = signal<string>('');

  async callBackend() {
    this.loading.set(true);
    this.error.set('');
    
    try {
      const response = await this.http.get<HelloResponse>('http://localhost:5000/api/chat/hello').toPromise();
      this.backendResponse.set(response?.message || 'No response');
    } catch (err) {
      this.error.set('Failed to connect to backend: ' + (err as any)?.message);
    } finally {
      this.loading.set(false);
    }
  }
}