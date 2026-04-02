import axios from 'axios';
import { API_BASE } from './baseurl';

export const apiClient = axios.create({
    baseURL: `${API_BASE}api/`,
    timeout: 10000,
});

apiClient.interceptors.request.use(config => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});
