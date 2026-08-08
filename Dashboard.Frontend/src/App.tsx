import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import KeywordsPage from './pages/discovery/KeywordsPage';
import DiscoveryJobsPage from './pages/discovery/JobsPage';
import VideosPage from './pages/collector/VideosPage';
import VideoDetailPage from './pages/collector/VideoDetailPage';
import CollectionJobsPage from './pages/collector/JobsPage';
import KnowledgeExtractionJobsPage from './pages/knowledgeExtraction/JobsPage';
import WorkflowPage from './pages/WorkflowPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="discovery/keywords" element={<KeywordsPage />} />
          <Route path="discovery/jobs" element={<DiscoveryJobsPage />} />
          <Route path="collector/videos" element={<VideosPage />} />
          <Route path="collector/videos/:id" element={<VideoDetailPage />} />
          <Route path="collector/jobs" element={<CollectionJobsPage />} />
          <Route path="knowledge-extraction/jobs" element={<KnowledgeExtractionJobsPage />} />
          <Route path="workflow" element={<WorkflowPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}