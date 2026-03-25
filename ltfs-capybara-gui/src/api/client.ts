import axios from 'axios';

export const apiClient = axios.create({
    baseURL: 'http://localhost:5003/api/',
    timeout: 10000,
});

apiClient.interceptors.request.use(config => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});
